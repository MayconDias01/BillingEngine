using BillingEngine.Application.Commands.Billing.TriggerBilling;
using BillingEngine.Application.Queries.GetBillingAttempts;
using MediatR;
using System.Security.Claims;

namespace BillingEngine.API.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/billing")
            .WithTags("Billing")
            .RequireAuthorization(policy => policy.RequireRole("user"));

        // userId extracted from JWT for IDOR: only the owner of the subscription
        // can trigger a charge against it.
        group.MapPost("/charge/{subscriptionId:guid}", async (
            Guid subscriptionId,
            HttpContext http,
            IMediator mediator) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            await mediator.Send(new TriggerBillingCommand(subscriptionId, userId));
            return Results.Accepted();
        })
        .WithName("TriggerBilling")
        .WithSummary("Dispara cobrança de uma assinatura")
        .WithDescription("Cria um Billing (Pending) e publica BillingRequested na fila. Idempotente: chamadas repetidas no mesmo dia retornam 202 sem criar duplicata.")
        .Produces(202)
        .Produces(400)
        .Produces(401)
        .Produces(404);

        // userId extracted from JWT for IDOR: only the owner of the subscription
        // can view its billing attempts.
        group.MapGet("/{subscriptionId:guid}/attempts", async (
            Guid subscriptionId,
            HttpContext http,
            IMediator mediator) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var attempts = await mediator.Send(
                new GetBillingAttemptsQuery(subscriptionId, userId));
            return Results.Ok(attempts);
        })
        .WithName("GetBillingAttempts")
        .WithSummary("Lista tentativas de cobrança de uma assinatura")
        .WithDescription("Retorna 404 (não 403) quando a assinatura pertence a outro usuário.")
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
