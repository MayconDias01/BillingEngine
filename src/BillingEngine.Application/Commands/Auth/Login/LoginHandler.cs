using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Exceptions;
using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Commands.Auth.Login;

public class LoginHandler : IRequestHandler<LoginCommand, string>
{
    // 16-byte salt (all-zero) + 32-byte hash (all-zero) in base64.
    // Ensures Argon2id always runs even when the user doesn't exist,
    // preventing timing-based user enumeration attacks.
    private const string DummyHash =
        "AAAAAAAAAAAAAAAAAAAAAA==:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public LoginHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens)
    {
        _users  = users;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<string> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct);

        // Always run hash verification — even for unknown emails — to
        // prevent timing-based enumeration of registered addresses.
        var storedHash = user?.PasswordHash ?? DummyHash;
        var valid = _hasher.Verify(request.Password, storedHash);

        if (user is null || !valid)
            throw new DomainException("Invalid credentials.");

        return _tokens.IssuePartialToken(user.Id);
    }
}
