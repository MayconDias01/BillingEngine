using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Queries.GetSubscription;

public class GetSubscriptionHandler : IRequestHandler<GetSubscriptionQuery, SubscriptionDto?>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ICustomerRepository _customers;

    public GetSubscriptionHandler(
        ISubscriptionRepository subscriptions,
        ICustomerRepository customers)
    {
        _subscriptions = subscriptions;
        _customers     = customers;
    }

    public async Task<SubscriptionDto?> Handle(GetSubscriptionQuery request, CancellationToken ct)
    {
        var subscription = await _subscriptions.GetByIdAsync(request.Id, ct);
        if (subscription is null) return null;

        // IDOR: verify the customer belongs to the requesting user;
        // return null (→ 404) to avoid confirming whether the resource exists.
        var customer = await _customers.GetByIdForUserAsync(
            subscription.CustomerId, request.UserId, ct);
        if (customer is null) return null;

        return new SubscriptionDto(
            subscription.Id,
            subscription.CustomerId,
            subscription.PlanId,
            subscription.Gateway.ToString(),
            subscription.Status.ToString(),
            subscription.NextBillingDate,
            subscription.CreatedAt);
    }
}
