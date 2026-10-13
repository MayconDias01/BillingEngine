using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Enums;
using BillingEngine.Domain.Events;
using BillingEngine.Domain.Exceptions;
using BillingEngine.Domain.Interfaces;
using MediatR;
using BillingEntity = BillingEngine.Domain.Entities.Billing;

namespace BillingEngine.Application.Commands.Billing.TriggerBilling;

public class TriggerBillingHandler : IRequestHandler<TriggerBillingCommand, Unit>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ICustomerRepository _customers;
    private readonly IPlanRepository _plans;
    private readonly IBillingRepository _billings;
    private readonly IMessageBus _messageBus;

    public TriggerBillingHandler(
        ISubscriptionRepository subscriptions,
        ICustomerRepository customers,
        IPlanRepository plans,
        IBillingRepository billings,
        IMessageBus messageBus)
    {
        _subscriptions = subscriptions;
        _customers     = customers;
        _plans         = plans;
        _billings      = billings;
        _messageBus    = messageBus;
    }

    public async Task<Unit> Handle(TriggerBillingCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptions.GetByIdAsync(request.SubscriptionId, ct)
            ?? throw new SubscriptionNotFoundException(request.SubscriptionId);

        // IDOR: when triggered from the API, verify the caller owns the subscription's customer.
        if (request.RequestingUserId.HasValue)
        {
            var customer = await _customers.GetByIdForUserAsync(
                subscription.CustomerId, request.RequestingUserId.Value, ct);
            if (customer is null)
                throw new SubscriptionNotFoundException(request.SubscriptionId);
        }

        if (subscription.Status != SubscriptionStatus.Active)
            throw new DomainException($"Subscription '{subscription.Id}' is not active.");

        var plan = await _plans.GetByIdAsync(subscription.PlanId, ct)
            ?? throw new DomainException($"Plan '{subscription.PlanId}' not found.");

        // Idempotency key scoped to subscription + billing date (prevents double-charge on same day).
        var idempotencyKey = $"billing:{subscription.Id}:{DateTime.UtcNow:yyyyMMdd}";

        var existing = await _billings.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing is not null)
            return Unit.Value; // already triggered today — idempotent

        var billing = BillingEntity.Create(
            subscription.Id,
            plan.Price,
            plan.Currency,
            subscription.NextBillingDate,
            idempotencyKey,
            subscription.Gateway);

        await _billings.AddAsync(billing, ct);

        await _messageBus.PublishAsync(
            new BillingRequested(subscription.Id, idempotencyKey, DateTime.UtcNow), ct);

        return Unit.Value;
    }
}
