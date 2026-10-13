using BillingEngine.Domain.Entities;

namespace BillingEngine.Domain.Interfaces;

public interface IPlanRepository
{
    Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<Plan>> GetAllActiveAsync(CancellationToken ct);
}
