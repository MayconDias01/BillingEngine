using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Exceptions;
using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Commands.Subscriptions.CreateSubscription;

public class CreateSubscriptionHandler : IRequestHandler<CreateSubscriptionCommand, Guid>
{
    private readonly ICustomerRepository _customers;
    private readonly IPlanRepository _plans;
    private readonly ISubscriptionRepository _subscriptions;

    public CreateSubscriptionHandler(
        ICustomerRepository customers,
        IPlanRepository plans,
        ISubscriptionRepository subscriptions)
    {
        _customers     = customers;
        _plans         = plans;
        _subscriptions = subscriptions;
    }

    public async Task<Guid> Handle(CreateSubscriptionCommand request, CancellationToken ct)
    {
        // IDOR: customer must belong to the authenticated user
        var customer = await _customers.GetByIdForUserAsync(request.CustomerId, request.UserId, ct)
            ?? throw new DomainException($"Customer '{request.CustomerId}' not found.");

        var plan = await _plans.GetByIdAsync(request.PlanId, ct)
            ?? throw new DomainException($"Plan '{request.PlanId}' not found.");

        if (!plan.IsActive)
            throw new DomainException($"Plan '{plan.Name}' is not active.");

        var subscription = Subscription.Create(
            customer.Id,
            plan.Id,
            request.Gateway,
            request.GatewayCustomerId,
            request.GatewayPaymentMethodId);

        // Align first billing date with the plan's billing cycle
        subscription.UpdateNextBillingDate(
            DateTime.UtcNow.AddDays(plan.BillingCycle.IntervalDays));

        await _subscriptions.AddAsync(subscription, ct);

        return subscription.Id;
    }
}
