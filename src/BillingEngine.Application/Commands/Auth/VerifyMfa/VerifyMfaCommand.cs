using BillingEngine.Application.DTOs;
using MediatR;

namespace BillingEngine.Application.Commands.Auth.VerifyMfa;

public record VerifyMfaCommand(Guid UserId, string Code) : IRequest<TokenPair>;
