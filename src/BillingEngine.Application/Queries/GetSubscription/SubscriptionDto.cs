namespace BillingEngine.Application.Queries.GetSubscription;

public record SubscriptionDto(
    Guid Id,
    Guid CustomerId,
    Guid PlanId,
    string Gateway,
    string Status,
    DateTime NextBillingDate,
    DateTime CreatedAt);
