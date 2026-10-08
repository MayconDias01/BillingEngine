using FluentValidation;

namespace BillingEngine.Application.Commands.Subscriptions.CreateSubscription;

public class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Gateway).IsInEnum();
        RuleFor(x => x.GatewayCustomerId).NotEmpty();
        RuleFor(x => x.GatewayPaymentMethodId).NotEmpty();
    }
}
