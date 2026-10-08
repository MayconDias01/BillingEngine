using System.Security.Cryptography;
using System.Text;
using BillingEngine.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BillingEngine.Infrastructure.Security;

/// <summary>
/// Cifra e decifra dados com AES-256-GCM (AEAD — authenticated encryption with associated data).
/// Cada chamada a <see cref="Encrypt"/> gera um nonce aleatório de 12 bytes via CSPRNG.
///
/// Formato do blob de saída (base64):
///   nonce (12 B) || ciphertext (N B) || authentication tag (16 B)
///
/// A tag autentica tanto o nonce quanto o ciphertext — qualquer adulteração é detectada
/// pelo <see cref="Decrypt"/> antes de retornar dados ao chamador.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private const int NonceSizeBytes = 12;   // 96 bits — recomendação NIST SP 800-38D
    private const int TagSizeBytes   = 16;   // 128 bits — tamanho máximo e recomendado
    private const int KeySizeBytes   = 32;   // 256 bits

    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Encryption:Key"];

        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new InvalidOperationException(
                "Encryption:Key não está configurado. " +
                "Gere a chave com: openssl rand -base64 32");

        _key = Convert.FromBase64String(keyBase64);

        if (_key.Length != KeySizeBytes)
            throw new InvalidOperationException(
                $"Encryption:Key deve conter exatamente {KeySizeBytes} bytes " +
                $"({KeySizeBytes * 8} bits) codificados em base64. " +
                $"Tamanho atual: {_key.Length} bytes.");
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce          = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext     = new byte[plaintextBytes.Length];
        var tag            = new byte[TagSizeBytes];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Layout: nonce (12 B) | ciphertext (N B) | tag (16 B)
        var result = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        nonce.CopyTo(result.AsSpan(0));
        ciphertext.CopyTo(result.AsSpan(NonceSizeBytes));
        tag.CopyTo(result.AsSpan(NonceSizeBytes + ciphertext.Length));

        return Convert.ToBase64String(result);
    }

    /// <inheritdoc />
    /// <exception cref="CryptographicException">
    /// Lançado se o blob estiver malformado ou se a tag de autenticação não bater
    /// (indicando adulteração ou chave incorreta).
    /// </exception>
    public string Decrypt(string ciphertextBase64)
    {
        byte[] data;
        try
        {
            data = Convert.FromBase64String(ciphertextBase64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Ciphertext inválido: base64 malformado.", ex);
        }

        var minLength = NonceSizeBytes + TagSizeBytes;
        if (data.Length < minLength)
            throw new CryptographicException(
                $"Ciphertext muito curto: esperado mínimo {minLength} bytes, recebido {data.Length}.");

        var nonce      = data.AsSpan(0, NonceSizeBytes);
        var tag        = data.AsSpan(data.Length - TagSizeBytes);
        var ciphertext = data.AsSpan(NonceSizeBytes, data.Length - NonceSizeBytes - TagSizeBytes);
        var plaintext  = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSizeBytes);

        // Lança AuthenticationTagMismatchException se o dado foi adulterado
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
