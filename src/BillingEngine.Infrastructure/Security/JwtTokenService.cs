using BillingEngine.Application.Interfaces;

namespace BillingEngine.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    public string IssuePartialToken(Guid userId)
        => throw new NotImplementedException();

    public string IssueAccessToken(Guid userId)
        => throw new NotImplementedException();

    public string GenerateRefreshToken()
        => throw new NotImplementedException();
}
