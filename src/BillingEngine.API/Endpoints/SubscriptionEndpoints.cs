using BillingEngine.Application.Commands.Subscriptions.CreateSubscription;
using BillingEngine.Application.Queries.GetSubscription;
using MediatR;

namespace BillingEngine.API.Endpoints;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/subscriptions")
            .RequireAuthorization(policy => policy.RequireRole("user"));

        group.MapPost("/", async (CreateSubscriptionCommand command, IMediator mediator) =>
        {
            var id = await mediator.Send(command);
            return Results.Created($"/subscriptions/{id}", new { id });
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSubscriptionQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });
    }
}
