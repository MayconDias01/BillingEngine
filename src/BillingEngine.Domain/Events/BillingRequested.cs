namespace BillingEngine.Domain.Events;

public record BillingRequested(Guid SubscriptionId, string IdempotencyKey, DateTime RequestedAt);
