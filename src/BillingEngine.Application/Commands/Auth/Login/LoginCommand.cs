using MediatR;

namespace BillingEngine.Application.Commands.Auth.Login;

public record LoginCommand(string Email, string Password) : IRequest<string>;
