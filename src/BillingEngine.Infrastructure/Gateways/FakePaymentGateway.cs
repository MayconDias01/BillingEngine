using BillingEngine.Application.DTOs;
using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Enums;

namespace BillingEngine.Infrastructure.Gateways;

public class FakePaymentGateway : IPaymentGateway
{
    public Task<GatewayResult> ChargeAsync(ChargeRequest request, CancellationToken ct)
    {
        var result = new GatewayResult(
            Success: true,
            TransactionId: Guid.NewGuid().ToString(),
            FailureCode: null,
            FailureReason: null,
            FailureType: FailureType.Transient);

        return Task.FromResult(result);
    }
}
