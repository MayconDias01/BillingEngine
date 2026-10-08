using System.Security.Cryptography;
using System.Text;
using BillingEngine.Application.Interfaces;
using Konscious.Security.Cryptography;

namespace BillingEngine.Infrastructure.Security;

/// <summary>
/// Implementa hash de senhas com Argon2id — primeira escolha do OWASP para hash de senhas.
/// Parâmetros seguem OWASP Password Storage Cheat Sheet (cenário interativo, 2024):
///   memória = 64 MB, iterações = 3, paralelismo = 1.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    // OWASP mínimo para logins interativos
    private const int MemorySizeKb        = 65_536;  // 64 MB
    private const int Iterations          = 3;
    private const int DegreeOfParallelism = 1;
    private const int HashLength          = 32;       // 256 bits de saída
    private const int SaltLength          = 16;       // 128 bits de salt

    /// <inheritdoc />
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = ComputeHash(password, salt);
        // Formato de armazenamento: base64(salt):base64(hash)
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc />
    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        byte[] salt, expected;
        try
        {
            salt     = Convert.FromBase64String(parts[0]);
            expected = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = ComputeHash(password, salt);

        // Comparação em tempo constante — previne timing attacks
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        using var argon2  = new Argon2id(passwordBytes);

        argon2.Salt                = salt;
        argon2.DegreeOfParallelism = DegreeOfParallelism;
        argon2.MemorySize          = MemorySizeKb;
        argon2.Iterations          = Iterations;

        return argon2.GetBytes(HashLength);
    }
}
