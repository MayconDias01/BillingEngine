namespace BillingEngine.Application.Interfaces;

public interface IIdempotencyService
{
    Task<bool> SetIfNotExistsAsync(string key, TimeSpan ttl, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
}
