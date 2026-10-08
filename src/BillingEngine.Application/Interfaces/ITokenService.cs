namespace BillingEngine.Application.Interfaces;

public interface ITokenService
{
    string IssuePartialToken(Guid userId);
    string IssueAccessToken(Guid userId);
    string GenerateRefreshToken();
}
