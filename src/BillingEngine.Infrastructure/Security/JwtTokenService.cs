using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BillingEngine.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BillingEngine.Infrastructure.Security;

/// <summary>
/// Emite tokens JWT assinados com RS256 (RSA + SHA-256 — PKCS#1 v1.5).
///
/// Tipos de token:
///   • Partial token  — role=mfa_pending, vida curta (emitido após login bem-sucedido,
///                       antes do TOTP ser verificado).
///   • Access token   — role=user, vida padrão (emitido após MFA verificado).
///   • Refresh token  — bytes aleatórios opacos; deve ser armazenado pelo handler
///                       (funcionalidade de armazenamento implementada no Passo 3).
///
/// Chave assimétrica:
///   Em produção, carrega a chave privada de um certificado X.509 (.pfx).
///   Em desenvolvimento (sem certificado configurado), gera uma chave RSA 2048-bit efêmera.
///   ATENÇÃO: a chave efêmera é perdida a cada reinício — tokens anteriores são invalidados.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly RsaSecurityKey _signingKey;    // chave privada — apenas para assinar
    private readonly RsaSecurityKey _validationKey; // chave pública — apenas para validar
    private readonly string         _issuer;
    private readonly int            _accessTokenMinutes;
    private readonly int            _partialTokenMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _issuer              = configuration["Auth:Issuer"] ?? "billing-engine";
        _accessTokenMinutes  = configuration.GetValue("Auth:AccessTokenExpiresMinutes", 15);
        _partialTokenMinutes = configuration.GetValue("Auth:PartialTokenExpiresMinutes", 5);

        (_signingKey, _validationKey) = LoadOrCreateKeys(configuration);
    }

    /// <inheritdoc />
    public string IssuePartialToken(Guid userId) =>
        BuildToken(userId, "mfa_pending", TimeSpan.FromMinutes(_partialTokenMinutes));

    /// <inheritdoc />
    public string IssueAccessToken(Guid userId) =>
        BuildToken(userId, "user", TimeSpan.FromMinutes(_accessTokenMinutes));

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        // 64 bytes = 512 bits de entropia — impossível de adivinhar por força bruta
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Expõe a chave pública RSA para configuração do middleware de validação JWT.
    /// Chamado durante a inicialização do contêiner DI — não expõe a chave privada.
    /// </summary>
    public RsaSecurityKey GetValidationKey() => _validationKey;

    // ──────────────────────────────────────────────────────────────── //

    private string BuildToken(Guid userId, string role, TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Issuer             = _issuer,
            IssuedAt           = now,
            NotBefore          = now,
            Expires            = now.Add(lifetime),
            SigningCredentials  = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(descriptor);
    }

    private static (RsaSecurityKey signing, RsaSecurityKey validation) LoadOrCreateKeys(
        IConfiguration configuration)
    {
        var certPath     = configuration["Auth:CertPath"];
        var certPassword = configuration["Auth:CertPassword"];

        RSA signingRsa;
        string keyId;

        if (!string.IsNullOrWhiteSpace(certPath) && File.Exists(certPath))
        {
            // X509CertificateLoader é a API recomendada no .NET 9 (substitui o construtor obsoleto)
            var cert = X509CertificateLoader.LoadPkcs12FromFile(
                certPath,
                certPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

            signingRsa = cert.GetRSAPrivateKey()
                ?? throw new InvalidOperationException(
                    $"O certificado '{certPath}' não contém chave privada RSA.");

            keyId = cert.Thumbprint;
        }
        else
        {
            // Desenvolvimento: chave efêmera em memória.
            // NÃO usar em produção — tokens não sobrevivem ao reinício da aplicação.
            signingRsa = RSA.Create(2048);
            keyId      = "dev-ephemeral";
        }

        // Exporta apenas os parâmetros públicos para o objeto de validação.
        // A chave privada nunca é exposta fora desta classe.
        var publicParams  = signingRsa.ExportParameters(includePrivateParameters: false);
        var validationRsa = RSA.Create();
        validationRsa.ImportParameters(publicParams);

        return (
            new RsaSecurityKey(signingRsa)    { KeyId = keyId },
            new RsaSecurityKey(validationRsa) { KeyId = keyId }
        );
    }
}
