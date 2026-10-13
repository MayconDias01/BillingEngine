using BillingEngine.Application.Queries.GetAllPlans;
using MediatR;

namespace BillingEngine.API.Endpoints;

public static class PlanEndpoints
{
    public static void MapPlanEndpoints(this WebApplication app)
    {
        app.MapGet("/plans", async (IMediator mediator, CancellationToken ct) =>
        {
            var plans = await mediator.Send(new GetAllPlansQuery(), ct);
            return Results.Ok(plans);
        })
        .WithName("GetAllPlans")
        .WithSummary("Lista todos os planos ativos")
        .WithDescription("Retorna Basic ($9.99/30d), Pro ($29.99/30d) e Enterprise ($299.99/365d).")
        .WithTags("Plans")
        .RequireAuthorization(policy => policy.RequireRole("user"))
        .Produces(200)
        .Produces(401);
    }
}
