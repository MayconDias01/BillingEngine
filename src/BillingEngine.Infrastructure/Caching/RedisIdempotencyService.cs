using BillingEngine.Application.Interfaces;

namespace BillingEngine.Infrastructure.Caching;

public class RedisIdempotencyService : IIdempotencyService
{
    public Task<bool> SetIfNotExistsAsync(string key, TimeSpan ttl, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<bool> ExistsAsync(string key, CancellationToken ct)
        => throw new NotImplementedException();
}
