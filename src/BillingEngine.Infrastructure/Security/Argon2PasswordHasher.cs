using BillingEngine.Application.Interfaces;

namespace BillingEngine.Infrastructure.Security;

public class Argon2PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
        => throw new NotImplementedException();

    public bool Verify(string password, string hash)
        => throw new NotImplementedException();
}
