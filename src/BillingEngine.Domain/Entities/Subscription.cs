using BillingEngine.Domain.Enums;

namespace BillingEngine.Domain.Entities;

public class Subscription
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid PlanId { get; private set; }
    public PaymentGateway Gateway { get; private set; }
    public string GatewayCustomerId { get; private set; } = default!;
    public string GatewayPaymentMethodId { get; private set; } = default!;
    public SubscriptionStatus Status { get; private set; }
    public DateTime NextBillingDate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Subscription() { }

    public static Subscription Create(
        Guid customerId,
        Guid planId,
        PaymentGateway gateway,
        string gatewayCustomerId,
        string gatewayPaymentMethodId)
    {
        return new Subscription
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            PlanId = planId,
            Gateway = gateway,
            GatewayCustomerId = gatewayCustomerId,
            GatewayPaymentMethodId = gatewayPaymentMethodId,
            Status = SubscriptionStatus.Active,
            NextBillingDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.Cancelled;
    }

    public void UpdateNextBillingDate(DateTime nextDate)
    {
        NextBillingDate = nextDate;
    }
}
