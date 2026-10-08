using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Interfaces;

namespace BillingEngine.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
        => throw new NotImplementedException();

    public Task AddAsync(User user, CancellationToken ct)
        => throw new NotImplementedException();
}
