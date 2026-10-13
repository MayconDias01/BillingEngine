using System.Security.Cryptography;
using System.Text;
using BillingEngine.Application.DTOs;
using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Exceptions;
using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Commands.Auth.Refresh;

public class RefreshHandler : IRequestHandler<RefreshCommand, TokenPair>
{
    private const int RefreshTokenExpiresDays = 7;

    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokens;

    public RefreshHandler(IRefreshTokenRepository refreshTokens, ITokenService tokens)
    {
        _refreshTokens = refreshTokens;
        _tokens        = tokens;
    }

    public async Task<TokenPair> Handle(RefreshCommand request, CancellationToken ct)
    {
        var tokenHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken)));

        var stored = await _refreshTokens.GetByTokenHashAsync(tokenHash, ct)
            ?? throw new DomainException("Refresh token not found.");

        if (!stored.IsValid())
            throw new DomainException("Refresh token is expired or revoked.");

        // Revoke the used token (rotation — each token is single-use)
        stored.Revoke();
        await _refreshTokens.UpdateAsync(stored, ct);

        var accessToken       = _tokens.IssueAccessToken(stored.UserId);
        var newRefreshPlain   = _tokens.GenerateRefreshToken();
        var newHash           = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(newRefreshPlain)));

        await _refreshTokens.AddAsync(
            RefreshToken.Create(stored.UserId, newHash, RefreshTokenExpiresDays), ct);

        return new TokenPair(accessToken, newRefreshPlain);
    }
}
