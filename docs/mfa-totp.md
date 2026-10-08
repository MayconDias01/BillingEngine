# MFA com TOTP — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-08

---

## 1. Objetivo

Descrever a implementação de autenticação multifator baseada em TOTP (Time-based
One-Time Password) e o ciclo de vida do segundo fator no BillingEngine.

---

## 2. Justificativa Técnica

TOTP (RFC 6238) foi escolhido como segundo fator porque:

- **Sem dependência de SMS:** evita SIM swapping e custo de gateway
- **Offline:** funciona sem conectividade (app local no dispositivo)
- **Amplamente suportado:** Google Authenticator, Authy, Microsoft Authenticator, 1Password, Bitwarden
- **Padrão aberto:** auditável e implementado em todas as linguagens
- **Segredo cifrado em repouso:** AES-256-GCM protege o segredo mesmo com vazamento do banco

---

## 3. Arquitetura

```
Registro
  ┌──────────────────────────────────────────────────────────────────┐
  │  TotpService.GenerateSecret()                                    │
  │    └─ RandomNumberGenerator.GetBytes(20) → Base32 (sem padding) │
  │                                                                  │
  │  TotpService.GetQrUri(secret, email)                             │
  │    └─ "otpauth://totp/BillingEngine:{email}?secret={secret}..."  │
  │                                                                  │
  │  AesEncryptionService.Encrypt(secret)                            │
  │    └─ armazenado em User.TotpSecret (cifrado)                    │
  └──────────────────────────────────────────────────────────────────┘

Verificação (MFA)
  ┌──────────────────────────────────────────────────────────────────┐
  │  AesEncryptionService.Decrypt(user.TotpSecret) → plainSecret     │
  │                                                                  │
  │  TotpService.Verify(plainSecret, code)                           │
  │    └─ Base32Decode(paddedSecret)                                 │
  │    └─ Totp.VerifyTotp(code, window=±1)                           │
  │         ├─ true  → emite access token                            │
  │         └─ false → 401 Unauthorized                              │
  └──────────────────────────────────────────────────────────────────┘
```

---

## 4. Regras de Negócio

1. **MFA é sempre obrigatório:** `User.IsMfaEnabled = true` na criação; não há forma de desativar
2. **Segredo gerado no servidor:** nunca enviado pelo cliente
3. **Segredo exposto uma única vez:** retornado no `RegisterResult`; após isso, só disponível cifrado no banco
4. **Janela de tolerância:** ±1 step (±30 s) — aceita 3 códigos consecutivos (passo anterior, atual, próximo)
5. **Proteção anti-replay:** (**Passo 3**) — o último código usado deve ser armazenado no Redis com TTL de 90 s e rejeitado em nova tentativa
6. **Código inválido é falha silenciosa:** nenhuma mensagem explica se o segredo ou o código estão errados
7. **MFA na API:** o endpoint `/auth/mfa/verify` aceita apenas tokens com role `mfa_pending`

---

## 5. Fluxo de Funcionamento

### Setup (Registro)

```
1. TotpService.GenerateSecret()
   → 20 bytes aleatórios (CSPRNG) → Base32 → "JBSWY3DPEHPK3PXP" (exemplo)

2. TotpService.GetQrUri("JBSWY3DPEHPK3PXP", "user@example.com")
   → "otpauth://totp/BillingEngine:user%40example.com?secret=JBSWY3DPEHPK3PXP
      &issuer=BillingEngine&algorithm=SHA1&digits=6&period=30"

3. AesEncryptionService.Encrypt("JBSWY3DPEHPK3PXP")
   → "abc123..." (base64 do blob cifrado)
   → Armazenado em User.TotpSecret

4. RegisterResult retorna { userId, totpSecret: "JBSWY3DPEHPK3PXP", qrUri: "otpauth://..." }
   → Usuário escaneia o QR code com seu autenticador
```

### Verificação (Login)

```
1. Usuário obtém partial token via POST /auth/login

2. POST /auth/mfa/verify
   Body: { userId: "<uuid>", code: "123456" }
   Authorization: Bearer <partial_token>

3. Handler:
   a. Extrai userId do claim "sub" do partial token (não do body)  ← Passo 3
   b. AesEncryptionService.Decrypt(user.TotpSecret) → plainSecret
   c. TotpService.Verify(plainSecret, "123456")
      → Base32Decode + HMAC-SHA1 + window ±1
   d. Em caso de sucesso: emite accessToken + refreshToken

4. Cliente armazena accessToken (memória/sessionStorage) e refreshToken (httpOnly cookie)
```

---

## 6. Parâmetros TOTP

| Parâmetro    | Valor    | Observação                                          |
|--------------|----------|-----------------------------------------------------|
| Algoritmo    | HMAC-SHA1| RFC 6238 padrão; compatível com todos os apps       |
| Dígitos      | 6        | Padrão universal                                    |
| Período      | 30 s     | Padrão universal                                    |
| Janela       | ±1 step  | Aceita ±30 s — compensação de drift de relógio      |
| Comprimento do segredo | 20 bytes (160 bits) | Mínimo RFC 4226; Otp.NET v1.4.1 |

---

## 7. Cuidados de Segurança

| Risco                         | Mitigação                                                          |
|-------------------------------|--------------------------------------------------------------------|
| Replay de código TOTP         | Armazenar último código usado no Redis com TTL=90s (Passo 3)      |
| Brute-force de código         | 10^6 combinações + rate limiting por userId (Passo 4)              |
| Vazamento do banco            | Segredo cifrado com AES-256-GCM — inútil sem a chave de criptografia |
| Segredo interceptado em trânsito | HTTPS obrigatório; segredo exposto apenas na resposta de registro |
| userId no body do /mfa/verify | Mover para claim `sub` do partial token (Passo 3)                 |
| Clock skew extremo            | ±1 step cobre ±30 s; NTP no servidor é necessário                 |

---

## 8. Pendências (Próximos Passos)

- [ ] **Passo 3:** Proteção anti-replay (Redis + TTL 90 s)
- [ ] **Passo 3:** Extrair `userId` do `sub` do partial token no `VerifyMfaHandler`
- [ ] **Passo 4:** Rate limiting em `/auth/mfa/verify` (máx. 5 tentativas por userId)
- [ ] **Futuro:** Códigos de recuperação (backup codes) — 8 códigos de uso único
- [ ] **Futuro:** Reconfiguração de MFA com autenticação elevada

---

## 9. Exemplos de Uso

```csharp
// Gerar segredo e QR URI (RegisterHandler)
var secret = _totpService.GenerateSecret();         // "JBSWY3DPEHPK3PXP..."
var qrUri  = _totpService.GetQrUri(secret, email);  // "otpauth://totp/..."
var encrypted = _encryptionService.Encrypt(secret); // cifrado para o banco

// Verificar código no login MFA (VerifyMfaHandler)
var plainSecret = _encryptionService.Decrypt(user.TotpSecret);
var isValid = _totpService.Verify(plainSecret, command.Code);
```

---

## 10. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                                  |
|------------|--------|----------------------|--------------------------------------------|
| 2026-10-08 | 1.0    | Equipe BillingEngine | Criação — TOTP implementado via Otp.NET    |
