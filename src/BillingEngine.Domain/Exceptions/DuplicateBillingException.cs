namespace BillingEngine.Domain.Exceptions;

public class DuplicateBillingException : DomainException
{
    public DuplicateBillingException(string idempotencyKey)
        : base($"A billing with idempotency key '{idempotencyKey}' already exists.") { }
}
