using MediatR;

namespace BillingEngine.Application.Commands.Billing.TriggerBilling;

public record TriggerBillingCommand(Guid SubscriptionId) : IRequest<Unit>;
