using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Queries.GetBillingAttempts;

public class GetBillingAttemptsHandler : IRequestHandler<GetBillingAttemptsQuery, IEnumerable<BillingAttemptDto>>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ICustomerRepository _customers;
    private readonly IBillingRepository _billings;

    public GetBillingAttemptsHandler(
        ISubscriptionRepository subscriptions,
        ICustomerRepository customers,
        IBillingRepository billings)
    {
        _subscriptions = subscriptions;
        _customers     = customers;
        _billings      = billings;
    }

    public async Task<IEnumerable<BillingAttemptDto>> Handle(
        GetBillingAttemptsQuery request, CancellationToken ct)
    {
        var subscription = await _subscriptions.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription is null) return [];

        // IDOR: verify the subscription's customer belongs to the requesting user.
        var customer = await _customers.GetByIdForUserAsync(
            subscription.CustomerId, request.UserId, ct);
        if (customer is null) return [];

        var attempts = await _billings.GetAttemptsBySubscriptionIdAsync(request.SubscriptionId, ct);

        return attempts.Select(a => new BillingAttemptDto(
            a.Id,
            a.BillingId,
            a.AttemptNumber,
            a.Status.ToString(),
            a.FailureReason,
            a.StartedAt,
            a.FinishedAt));
    }
}
