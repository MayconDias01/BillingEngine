using FluentValidation;

namespace BillingEngine.Application.Commands.Billing.TriggerBilling;

public class TriggerBillingValidator : AbstractValidator<TriggerBillingCommand>
{
    public TriggerBillingValidator()
    {
        RuleFor(x => x.SubscriptionId).NotEmpty();
    }
}
