using BillingEngine.Domain.Enums;
using MediatR;

namespace BillingEngine.Application.Commands.Subscriptions.CreateSubscription;

public record CreateSubscriptionCommand(
    Guid UserId,
    Guid CustomerId,
    Guid PlanId,
    PaymentGateway Gateway,
    string GatewayCustomerId,
    string GatewayPaymentMethodId) : IRequest<Guid>;
