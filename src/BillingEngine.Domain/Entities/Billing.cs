using BillingEngine.Domain.Enums;

namespace BillingEngine.Domain.Entities;

public class Billing
{
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateTime DueDate { get; private set; }
    public BillingStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public PaymentGateway Gateway { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }

    private Billing() { }

    public static Billing Create(
        Guid subscriptionId,
        decimal amount,
        string currency,
        DateTime dueDate,
        string idempotencyKey,
        PaymentGateway gateway)
    {
        return new Billing
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            Amount = amount,
            Currency = currency,
            DueDate = dueDate,
            Status = BillingStatus.Pending,
            IdempotencyKey = idempotencyKey,
            Gateway = gateway,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsPaid()
    {
        Status = BillingStatus.Paid;
        PaidAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = BillingStatus.Failed;
    }
}
