using BillingEngine.Application.Commands.Billing.TriggerBilling;
using BillingEngine.Application.Queries.GetBillingAttempts;
using MediatR;

namespace BillingEngine.API.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/billing")
            .RequireAuthorization(policy => policy.RequireRole("user"));

        group.MapPost("/charge/{subscriptionId:guid}", async (Guid subscriptionId, IMediator mediator) =>
        {
            await mediator.Send(new TriggerBillingCommand(subscriptionId));
            return Results.Accepted();
        });

        group.MapGet("/{subscriptionId:guid}/attempts", async (Guid subscriptionId, IMediator mediator) =>
        {
            var attempts = await mediator.Send(new GetBillingAttemptsQuery(subscriptionId));
            return Results.Ok(attempts);
        });
    }
}
