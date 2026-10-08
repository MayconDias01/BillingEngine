using BillingEngine.Domain.Enums;

namespace BillingEngine.Domain.Entities;

public class BillingAttempt
{
    public Guid Id { get; private set; }
    public Guid BillingId { get; private set; }
    public int AttemptNumber { get; private set; }
    public PaymentGateway Gateway { get; private set; }
    public AttemptStatus Status { get; private set; }
    public string? GatewayTransactionId { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    private BillingAttempt() { }

    public static BillingAttempt Create(Guid billingId, int attemptNumber, PaymentGateway gateway)
    {
        return new BillingAttempt
        {
            Id = Guid.NewGuid(),
            BillingId = billingId,
            AttemptNumber = attemptNumber,
            Gateway = gateway,
            Status = AttemptStatus.Processing,
            StartedAt = DateTime.UtcNow
        };
    }

    public void Complete(string gatewayTransactionId)
    {
        Status = AttemptStatus.Succeeded;
        GatewayTransactionId = gatewayTransactionId;
        FinishedAt = DateTime.UtcNow;
    }

    public void Fail(string failureCode, string failureReason)
    {
        Status = AttemptStatus.Failed;
        FailureCode = failureCode;
        FailureReason = failureReason;
        FinishedAt = DateTime.UtcNow;
    }
}
