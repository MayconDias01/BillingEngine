using System.Security.Cryptography;
using System.Text;
using BillingEngine.Application.DTOs;
using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Exceptions;
using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Commands.Auth.VerifyMfa;

public class VerifyMfaHandler : IRequestHandler<VerifyMfaCommand, TokenPair>
{
    private const int RefreshTokenExpiresDays = 7;

    private readonly IUserRepository _users;
    private readonly ITotpService _totp;
    private readonly IEncryptionService _encryption;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;

    public VerifyMfaHandler(
        IUserRepository users,
        ITotpService totp,
        IEncryptionService encryption,
        ITokenService tokens,
        IRefreshTokenRepository refreshTokens)
    {
        _users         = users;
        _totp          = totp;
        _encryption    = encryption;
        _tokens        = tokens;
        _refreshTokens = refreshTokens;
    }

    public async Task<TokenPair> Handle(VerifyMfaCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new DomainException("User not found.");

        var totpSecret = _encryption.Decrypt(user.TotpSecret);
        if (!_totp.Verify(totpSecret, request.Code))
            throw new DomainException("Invalid or expired MFA code.");

        var accessToken       = _tokens.IssueAccessToken(user.Id);
        var refreshTokenPlain = _tokens.GenerateRefreshToken();

        var tokenHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshTokenPlain)));

        var refreshToken = RefreshToken.Create(user.Id, tokenHash, RefreshTokenExpiresDays);
        await _refreshTokens.AddAsync(refreshToken, ct);

        return new TokenPair(accessToken, refreshTokenPlain);
    }
}
