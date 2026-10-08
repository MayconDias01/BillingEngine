using MediatR;

namespace BillingEngine.Application.Queries.GetBillingAttempts;

public class GetBillingAttemptsHandler : IRequestHandler<GetBillingAttemptsQuery, IEnumerable<BillingAttemptDto>>
{
    public Task<IEnumerable<BillingAttemptDto>> Handle(GetBillingAttemptsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
