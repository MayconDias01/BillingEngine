using MediatR;

namespace BillingEngine.Application.Queries.GetSubscription;

public record GetSubscriptionQuery(Guid Id, Guid UserId) : IRequest<SubscriptionDto?>;
