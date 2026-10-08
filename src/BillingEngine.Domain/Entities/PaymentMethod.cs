using BillingEngine.Domain.Enums;

namespace BillingEngine.Domain.Entities;

public class PaymentMethod
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public PaymentGateway Gateway { get; private set; }
    public string GatewayCustomerId { get; private set; } = default!;
    public string GatewayPaymentMethodId { get; private set; } = default!;
    public bool IsDefault { get; private set; }

    private PaymentMethod() { }

    public static PaymentMethod Create(
        Guid customerId,
        PaymentGateway gateway,
        string gatewayCustomerId,
        string gatewayPaymentMethodId)
    {
        return new PaymentMethod
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Gateway = gateway,
            GatewayCustomerId = gatewayCustomerId,
            GatewayPaymentMethodId = gatewayPaymentMethodId,
            IsDefault = false
        };
    }
}
