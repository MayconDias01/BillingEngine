using MediatR;

namespace BillingEngine.Application.Commands.Auth.Register;

public record RegisterCommand(string Email, string Password) : IRequest<RegisterResult>;
