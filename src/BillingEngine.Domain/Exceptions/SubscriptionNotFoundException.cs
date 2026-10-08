namespace BillingEngine.Domain.Exceptions;

public class SubscriptionNotFoundException : DomainException
{
    public SubscriptionNotFoundException(Guid subscriptionId)
        : base($"Subscription with ID '{subscriptionId}' was not found.") { }
}
