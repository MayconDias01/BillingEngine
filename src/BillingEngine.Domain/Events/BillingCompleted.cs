namespace BillingEngine.Domain.Events;

public record BillingCompleted(Guid BillingId, bool Success, DateTime CompletedAt);
