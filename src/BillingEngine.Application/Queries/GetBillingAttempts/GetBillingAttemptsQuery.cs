using MediatR;

namespace BillingEngine.Application.Queries.GetBillingAttempts;

public record GetBillingAttemptsQuery(Guid SubscriptionId) : IRequest<IEnumerable<BillingAttemptDto>>;
