namespace BillingEngine.Application.DTOs;

public record ChargeRequest(
    Guid SubscriptionId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string GatewayCustomerId,
    string GatewayPaymentMethodId);
