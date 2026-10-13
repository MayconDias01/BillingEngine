using BillingEngine.Application.Commands.Subscriptions.CreateSubscription;
using BillingEngine.Application.Queries.GetSubscription;
using BillingEngine.Domain.Enums;
using MediatR;
using System.Security.Claims;

namespace BillingEngine.API.Endpoints;

public static class SubscriptionEndpoints
{
    private record CreateSubscriptionRequest(
        Guid CustomerId,
        Guid PlanId,
        PaymentGateway Gateway,
        string GatewayCustomerId,
        string GatewayPaymentMethodId);

    public static void MapSubscriptionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/subscriptions")
            .WithTags("Subscriptions")
            .RequireAuthorization(policy => policy.RequireRole("user"));

        // userId extracted from JWT sub claim — not from the request body —
        // to enforce ownership: a user can only create subscriptions for their own customers.
        group.MapPost("/", async (
            CreateSubscriptionRequest body,
            HttpContext http,
            IMediator mediator) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var command = new CreateSubscriptionCommand(
                userId,
                body.CustomerId,
                body.PlanId,
                body.Gateway,
                body.GatewayCustomerId,
                body.GatewayPaymentMethodId);

            var id = await mediator.Send(command);
            return Results.Created($"/subscriptions/{id}", new { id });
        })
        .WithName("CreateSubscription")
        .WithSummary("Cria uma assinatura")
        .WithDescription("Associa um Customer a um Plan, define o gateway e calcula a primeira NextBillingDate.")
        .Produces(201)
        .ProducesValidationProblem(422)
        .Produces(400)
        .Produces(401);

        // userId extracted from JWT sub claim for IDOR protection:
        // a subscription is only returned if the caller owns the related customer.
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext http,
            IMediator mediator) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var result = await mediator.Send(new GetSubscriptionQuery(id, userId));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetSubscription")
        .WithSummary("Retorna uma assinatura pelo ID")
        .WithDescription("Retorna 404 (não 403) quando a assinatura pertence a outro usuário — evita confirmar a existência do recurso.")
        .Produces(200)
        .Produces(401)
        .Produces(404);
    }

    private static bool TryGetUserId(HttpContext http, out Guid userId)
    {
        var sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? http.User.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }
}
