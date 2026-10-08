using MediatR;

namespace BillingEngine.Application.Commands.Subscriptions.CreateSubscription;

public class CreateSubscriptionHandler : IRequestHandler<CreateSubscriptionCommand, Guid>
{
    public Task<Guid> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
