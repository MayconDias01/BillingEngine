using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Interfaces;

namespace BillingEngine.Infrastructure.Persistence.Repositories;

public class BillingRepository : IBillingRepository
{
    public Task<Billing?> GetByIdAsync(Guid id, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<Billing?> GetByIdempotencyKeyAsync(string key, CancellationToken ct)
        => throw new NotImplementedException();

    public Task AddAsync(Billing billing, CancellationToken ct)
        => throw new NotImplementedException();

    public Task UpdateAsync(Billing billing, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IEnumerable<BillingAttempt>> GetAttemptsByBillingIdAsync(Guid billingId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task AddAttemptAsync(BillingAttempt attempt, CancellationToken ct)
        => throw new NotImplementedException();

    public Task UpdateAttemptAsync(BillingAttempt attempt, CancellationToken ct)
        => throw new NotImplementedException();
}
