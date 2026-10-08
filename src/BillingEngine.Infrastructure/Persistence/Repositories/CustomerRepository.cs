using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Interfaces;

namespace BillingEngine.Infrastructure.Persistence.Repositories;

public class CustomerRepository : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct)
        => throw new NotImplementedException();

    public Task AddAsync(Customer customer, CancellationToken ct)
        => throw new NotImplementedException();
}
