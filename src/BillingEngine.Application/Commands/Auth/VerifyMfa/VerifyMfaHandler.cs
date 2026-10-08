using BillingEngine.Application.DTOs;
using MediatR;

namespace BillingEngine.Application.Commands.Auth.VerifyMfa;

public class VerifyMfaHandler : IRequestHandler<VerifyMfaCommand, TokenPair>
{
    public Task<TokenPair> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
