using BillingEngine.Application.Interfaces;
using StackExchange.Redis;

namespace BillingEngine.Infrastructure.Caching;

public class RedisIdempotencyService : IIdempotencyService
{
    private readonly IDatabase _redis;

    public RedisIdempotencyService(IConnectionMultiplexer redis) =>
        _redis = redis.GetDatabase();

    // Returns true if the key was newly set (this is the first time we see it).
    // Returns false if the key already existed (duplicate — skip processing).
    public async Task<bool> SetIfNotExistsAsync(string key, TimeSpan ttl, CancellationToken ct) =>
        await _redis.StringSetAsync(key, "1", ttl, When.NotExists);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct) =>
        await _redis.KeyExistsAsync(key);
}
