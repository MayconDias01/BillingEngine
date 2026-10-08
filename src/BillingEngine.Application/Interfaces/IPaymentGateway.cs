using BillingEngine.Application.DTOs;

namespace BillingEngine.Application.Interfaces;

public interface IPaymentGateway
{
    Task<GatewayResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
}
