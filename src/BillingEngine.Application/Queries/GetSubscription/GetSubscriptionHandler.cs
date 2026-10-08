using MediatR;

namespace BillingEngine.Application.Queries.GetSubscription;

public class GetSubscriptionHandler : IRequestHandler<GetSubscriptionQuery, SubscriptionDto?>
{
    public Task<SubscriptionDto?> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
