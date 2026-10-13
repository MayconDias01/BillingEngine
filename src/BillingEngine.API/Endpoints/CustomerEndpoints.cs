using BillingEngine.Application.Commands.Customers.CreateCustomer;
using MediatR;
using System.Security.Claims;

namespace BillingEngine.API.Endpoints;

public static class CustomerEndpoints
{
    private record CreateCustomerRequest(string Name, string Email, string Document);

    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/customers")
            .WithTags("Customers")
            .RequireAuthorization(policy => policy.RequireRole("user"));

        // userId extracted from JWT sub claim — each customer belongs to exactly one user.
        group.MapPost("/", async (
            CreateCustomerRequest body,
            HttpContext http,
            IMediator mediator) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var id = await mediator.Send(
                new CreateCustomerCommand(userId, body.Name, body.Email, body.Document));

            return Results.Created($"/customers/{id}", new { id });
        })
        .WithName("CreateCustomer")
        .WithSummary("Cria um customer")
        .WithDescription("O documento é criptografado com AES-256-GCM antes de persistir. O Customer fica vinculado ao userId do JWT.")
        .Produces(201)
        .ProducesValidationProblem(422)
        .Produces(401);
    }

    private static bool TryGetUserId(HttpContext http, out Guid userId)
    {
        var sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? http.User.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }
}
