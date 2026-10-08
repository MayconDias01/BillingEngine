using BillingEngine.Application.DTOs;
using MediatR;

namespace BillingEngine.Application.Commands.Auth.Refresh;

public class RefreshHandler : IRequestHandler<RefreshCommand, TokenPair>
{
    public Task<TokenPair> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
