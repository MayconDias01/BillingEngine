using MediatR;

namespace BillingEngine.Application.Queries.GetBillingAttempts;

public record GetBillingAttemptsQuery(Guid SubscriptionId, Guid UserId) : IRequest<IEnumerable<BillingAttemptDto>>;
