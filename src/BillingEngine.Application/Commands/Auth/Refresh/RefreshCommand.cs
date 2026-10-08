using BillingEngine.Application.DTOs;
using MediatR;

namespace BillingEngine.Application.Commands.Auth.Refresh;

public record RefreshCommand(string RefreshToken) : IRequest<TokenPair>;
