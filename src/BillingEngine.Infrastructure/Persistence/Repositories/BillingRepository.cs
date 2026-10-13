using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingEngine.Infrastructure.Persistence.Repositories;

public class BillingRepository : IBillingRepository
{
    private readonly AppDbContext _db;

    public BillingRepository(AppDbContext db) => _db = db;

    public async Task<Billing?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await _db.Billings.FindAsync([id], ct);

    public Task<Billing?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) =>
        _db.Billings.FirstOrDefaultAsync(b => b.IdempotencyKey == key, ct);

    public async Task AddAsync(Billing billing, CancellationToken ct)
    {
        await _db.Billings.AddAsync(billing, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Billing billing, CancellationToken ct)
    {
        _db.Billings.Update(billing);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<BillingAttempt>> GetAttemptsByBillingIdAsync(Guid billingId, CancellationToken ct) =>
        await _db.BillingAttempts
                 .Where(a => a.BillingId == billingId)
                 .OrderBy(a => a.AttemptNumber)
                 .ToListAsync(ct);

    public async Task<IEnumerable<BillingAttempt>> GetAttemptsBySubscriptionIdAsync(
        Guid subscriptionId, CancellationToken ct) =>
        await _db.BillingAttempts
                 .Where(a => _db.Billings
                     .Where(b => b.SubscriptionId == subscriptionId)
                     .Select(b => b.Id)
                     .Contains(a.BillingId))
                 .OrderBy(a => a.AttemptNumber)
                 .ToListAsync(ct);

    public async Task AddAttemptAsync(BillingAttempt attempt, CancellationToken ct)
    {
        await _db.BillingAttempts.AddAsync(attempt, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAttemptAsync(BillingAttempt attempt, CancellationToken ct)
    {
        _db.BillingAttempts.Update(attempt);
        await _db.SaveChangesAsync(ct);
    }
}
