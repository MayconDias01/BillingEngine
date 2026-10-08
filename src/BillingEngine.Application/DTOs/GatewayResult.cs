using BillingEngine.Domain.Enums;

namespace BillingEngine.Application.DTOs;

public record GatewayResult(
    bool Success,
    string? TransactionId,
    string? FailureCode,
    string? FailureReason,
    FailureType FailureType);
