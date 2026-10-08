using BillingEngine.Application.Interfaces;

namespace BillingEngine.Infrastructure.Security;

public class AesEncryptionService : IEncryptionService
{
    public string Encrypt(string plaintext)
        => throw new NotImplementedException();

    public string Decrypt(string ciphertext)
        => throw new NotImplementedException();
}
