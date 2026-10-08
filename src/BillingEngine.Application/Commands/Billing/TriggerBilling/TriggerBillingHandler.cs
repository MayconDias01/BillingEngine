using MediatR;

namespace BillingEngine.Application.Commands.Billing.TriggerBilling;

public class TriggerBillingHandler : IRequestHandler<TriggerBillingCommand, Unit>
{
    public Task<Unit> Handle(TriggerBillingCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
