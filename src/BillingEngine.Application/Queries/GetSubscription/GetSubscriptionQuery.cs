using MediatR;

namespace BillingEngine.Application.Queries.GetSubscription;

public record GetSubscriptionQuery(Guid Id) : IRequest<SubscriptionDto?>;
