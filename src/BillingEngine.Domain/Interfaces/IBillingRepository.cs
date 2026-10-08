using BillingEngine.Domain.Entities;

namespace BillingEngine.Domain.Interfaces;

public interface IBillingRepository
{
    Task<Billing?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Billing?> GetByIdempotencyKeyAsync(string key, CancellationToken ct);
    Task AddAsync(Billing billing, CancellationToken ct);
    Task UpdateAsync(Billing billing, CancellationToken ct);
    Task<IEnumerable<BillingAttempt>> GetAttemptsByBillingIdAsync(Guid billingId, CancellationToken ct);
    Task AddAttemptAsync(BillingAttempt attempt, CancellationToken ct);
    Task UpdateAttemptAsync(BillingAttempt attempt, CancellationToken ct);
}
