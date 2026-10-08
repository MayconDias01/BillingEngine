using System.Security.Cryptography;
using BillingEngine.Application.Interfaces;
using OtpNet;

namespace BillingEngine.Infrastructure.Security;

/// <summary>
/// Implementa TOTP (Time-based One-Time Password — RFC 6238) para autenticação multifator.
///
/// Parâmetros compatíveis com todos os principais autenticadores
/// (Google Authenticator, Authy, Microsoft Authenticator, 1Password, etc.):
///   algoritmo = HMAC-SHA1, dígitos = 6, período = 30 s.
///
/// Tolerância de janela: ±1 step (±30 s) para compensar drift de relógio entre
/// cliente e servidor. Implementação de proteção contra replay deve ser adicionada
/// no Passo 3 (armazenar o último código usado em Redis com TTL de 90 s).
/// </summary>
public sealed class TotpService : ITotpService
{
    private const string AppIssuer   = "BillingEngine";
    private const int    SecretBytes = 20;  // 160 bits — mínimo recomendado por RFC 4226
    private const int    Window      = 1;   // Aceita ±1 step (±30 s)

    /// <inheritdoc />
    /// <returns>Segredo em Base32 sem padding, pronto para QR code e armazenamento cifrado.</returns>
    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretBytes);
        // Base32 sem padding '=' — padrão adotado pela maioria dos autenticadores
        return Base32Encoding.ToString(bytes).TrimEnd('=');
    }

    /// <inheritdoc />
    /// <returns>URI no formato otpauth:// conforme Google Key URI Format.</returns>
    public string GetQrUri(string secret, string email)
    {
        var issuer  = Uri.EscapeDataString(AppIssuer);
        var account = Uri.EscapeDataString(email);
        // Formato: otpauth://totp/<issuer>:<account>?secret=...&issuer=...
        return $"otpauth://totp/{issuer}:{account}" +
               $"?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
    }

    /// <inheritdoc />
    /// <remarks>
    /// Falha silenciosa em caso de secret inválido — não revela a razão da falha ao chamador,
    /// evitando information leakage sobre o formato esperado.
    /// </remarks>
    public bool Verify(string secret, string code)
    {
        // Validação rápida antes de processar
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (code.Length != 6)               return false;
        if (!code.All(char.IsDigit))        return false;

        try
        {
            // Base32 exige comprimento múltiplo de 8 — adiciona padding se necessário
            var padded      = secret.ToUpperInvariant().PadRight((secret.Length + 7) / 8 * 8, '=');
            var secretBytes = Base32Encoding.ToBytes(padded);

            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(Window, Window));
        }
        catch
        {
            // Secret malformado — trata como verificação falsa
            return false;
        }
    }
}
