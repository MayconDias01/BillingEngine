using BillingEngine.Application.Commands.Auth.Login;
using BillingEngine.Application.Commands.Auth.Refresh;
using BillingEngine.Application.Commands.Auth.Register;
using BillingEngine.Application.Commands.Auth.VerifyMfa;
using MediatR;

namespace BillingEngine.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (RegisterCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/auth/{result.UserId}", result);
        });

        group.MapPost("/login", async (LoginCommand command, IMediator mediator) =>
        {
            var partialToken = await mediator.Send(command);
            return Results.Ok(new { partialToken });
        });

        group.MapPost("/mfa/verify", async (VerifyMfaCommand command, IMediator mediator) =>
        {
            var tokenPair = await mediator.Send(command);
            return Results.Ok(tokenPair);
        }).RequireAuthorization(policy => policy.RequireRole("mfa_pending"));

        group.MapPost("/refresh", async (RefreshCommand command, IMediator mediator) =>
        {
            var tokenPair = await mediator.Send(command);
            return Results.Ok(tokenPair);
        });
    }
}
