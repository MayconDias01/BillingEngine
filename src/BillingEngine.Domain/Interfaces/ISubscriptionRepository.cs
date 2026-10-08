using BillingEngine.Domain.Entities;

namespace BillingEngine.Domain.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Subscription subscription, CancellationToken ct);
    Task UpdateAsync(Subscription subscription, CancellationToken ct);
    Task<IEnumerable<Subscription>> GetDueForBillingAsync(DateTime asOf, CancellationToken ct);
}
