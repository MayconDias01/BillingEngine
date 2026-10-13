using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Exceptions;
using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Commands.Auth.Register;

public class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITotpService _totp;
    private readonly IEncryptionService _encryption;

    public RegisterHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        ITotpService totp,
        IEncryptionService encryption)
    {
        _users = users;
        _hasher = hasher;
        _totp = totp;
        _encryption = encryption;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existing = await _users.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            throw new DomainException("Email already registered.");

        var passwordHash = _hasher.Hash(request.Password);
        var totpSecret   = _totp.GenerateSecret();
        var qrUri        = _totp.GetQrUri(totpSecret, request.Email);
        var encryptedSecret = _encryption.Encrypt(totpSecret);

        var user = User.Create(request.Email, passwordHash, encryptedSecret);
        await _users.AddAsync(user, ct);

        return new RegisterResult(user.Id, totpSecret, qrUri);
    }
}
