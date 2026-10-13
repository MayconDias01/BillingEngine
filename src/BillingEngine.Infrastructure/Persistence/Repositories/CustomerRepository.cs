using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingEngine.Infrastructure.Persistence.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;

    public CustomerRepository(AppDbContext db) => _db = db;

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await _db.Customers.FindAsync([id], ct);

    public Task<Customer?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct)
    {
        await _db.Customers.AddAsync(customer, ct);
        await _db.SaveChangesAsync(ct);
    }
}
