using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Interfaces;

namespace BillingEngine.Infrastructure.Persistence.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct)
        => throw new NotImplementedException();

    public Task AddAsync(Subscription subscription, CancellationToken ct)
        => throw new NotImplementedException();

    public Task UpdateAsync(Subscription subscription, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IEnumerable<Subscription>> GetDueForBillingAsync(DateTime asOf, CancellationToken ct)
        => throw new NotImplementedException();
}
