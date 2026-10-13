using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingEngine.Infrastructure.Persistence.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly AppDbContext _db;

    public PlanRepository(AppDbContext db) => _db = db;

    public Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Plan>> GetAllActiveAsync(CancellationToken ct) =>
        await _db.Plans.Where(p => p.IsActive).ToListAsync(ct);
}
