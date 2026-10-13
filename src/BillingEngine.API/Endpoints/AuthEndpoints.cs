using BillingEngine.Application.Commands.Auth.Login;
using BillingEngine.Application.Commands.Auth.Refresh;
using BillingEngine.Application.Commands.Auth.Register;
using BillingEngine.Application.Commands.Auth.VerifyMfa;
using MediatR;
using System.Security.Claims;

namespace BillingEngine.API.Endpoints;

public static class AuthEndpoints
{
    private record VerifyMfaRequest(string Code);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return Results.Created($"/auth/{result.UserId}", result);
        })
        .WithName("Register")
        .WithSummary("Registra um novo usuário")
        .WithDescription("Cria conta, gera segredo TOTP e retorna QR code URI para configuração do autenticador.")
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .Produces(201)
        .ProducesValidationProblem(422)
        .Produces(400);

        group.MapPost("/login", async (LoginCommand command, IMediator mediator) =>
        {
            var partialToken = await mediator.Send(command);
            return Results.Ok(new { partialToken });
        })
        .WithName("Login")
        .WithSummary("Autentica com e-mail e senha")
        .WithDescription("Retorna token parcial (role=mfa_pending, 5 min) para ser usado em /auth/mfa/verify.")
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .Produces(200)
        .ProducesValidationProblem(422)
        .Produces(400);

        // userId extracted from partial JWT sub claim — not from the request body —
        // to prevent IDOR: a user cannot verify MFA for a different account.
        group.MapPost("/mfa/verify", async (
            VerifyMfaRequest body,
            HttpContext http,
            IMediator mediator) =>
        {
            var sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? http.User.FindFirstValue("sub");

            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();

            var tokenPair = await mediator.Send(new VerifyMfaCommand(userId, body.Code));
            return Results.Ok(tokenPair);
        })
        .WithName("VerifyMfa")
        .WithSummary("Verifica código TOTP e emite token de acesso")
        .WithDescription("Requer o token parcial de /auth/login. Retorna access token (15 min) e refresh token (7 dias).")
        .RequireAuthorization(policy => policy.RequireRole("mfa_pending"))
        .Produces(200)
        .Produces(400)
        .Produces(401);

        group.MapPost("/refresh", async (RefreshCommand command, IMediator mediator) =>
        {
            var tokenPair = await mediator.Send(command);
            return Results.Ok(tokenPair);
        })
        .WithName("RefreshToken")
        .WithSummary("Renova o par de tokens")
        .WithDescription("Revoga o refresh token atual e emite um novo par (single-use rotation).")
        .AllowAnonymous()
        .Produces(200)
        .ProducesValidationProblem(422)
        .Produces(400);
    }
}
