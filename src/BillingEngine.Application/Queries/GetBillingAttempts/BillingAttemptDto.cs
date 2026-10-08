namespace BillingEngine.Application.Queries.GetBillingAttempts;

public record BillingAttemptDto(
    Guid Id,
    Guid BillingId,
    int AttemptNumber,
    string Status,
    string? FailureReason,
    DateTime StartedAt,
    DateTime? FinishedAt);
