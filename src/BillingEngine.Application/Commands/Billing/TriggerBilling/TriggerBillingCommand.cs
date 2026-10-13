using MediatR;

namespace BillingEngine.Application.Commands.Billing.TriggerBilling;

// RequestingUserId: present when triggered from the API (enables IDOR check).
// Null when triggered by the Worker scanner loop (system context, no ownership check).
public record TriggerBillingCommand(Guid SubscriptionId, Guid? RequestingUserId = null) : IRequest<Unit>;
