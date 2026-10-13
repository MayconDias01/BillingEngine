using BillingEngine.Domain.Entities;

namespace BillingEngine.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Customer?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct);
    Task AddAsync(Customer customer, CancellationToken ct);
}
