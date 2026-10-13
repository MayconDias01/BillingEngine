using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Enums;
using BillingEngine.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingEngine.Infrastructure.Persistence.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _db;

    public SubscriptionRepository(AppDbContext db) => _db = db;

    public async Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await _db.Subscriptions.FindAsync([id], ct);

    public async Task AddAsync(Subscription subscription, CancellationToken ct)
    {
        await _db.Subscriptions.AddAsync(subscription, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Subscription subscription, CancellationToken ct)
    {
        _db.Subscriptions.Update(subscription);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Retorna todas as assinaturas ativas com NextBillingDate &lt;= asOf.
    /// Usado pelo Worker para varrer cobranças vencidas.
    /// </summary>
    public async Task<IEnumerable<Subscription>> GetDueForBillingAsync(DateTime asOf, CancellationToken ct) =>
        await _db.Subscriptions
                 .Where(s => s.Status == SubscriptionStatus.Active && s.NextBillingDate <= asOf)
                 .ToListAsync(ct);
}
