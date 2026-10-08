# Autenticação — BillingEngine

> **Status:** Passo 1 implementado · **Versão:** 1.0 · **Última revisão:** 2026-10-08

---

## 1. Objetivo

Documentar todos os fluxos de autenticação do BillingEngine: registro, login com MFA
obrigatório, renovação de sessão e as regras de negócio que governam cada etapa.

---

## 2. Justificativa Técnica

O MFA é **obrigatório** desde o registro porque o sistema processa transações financeiras.
Toda nova conta recebe automaticamente um segredo TOTP — o usuário precisa configurar o
autenticador antes de poder obter um access token válido. Isso garante que não existam
contas sem segundo fator ativo.

---

## 3. Arquitetura

```
Cliente                    API (Minimal API)            Application (MediatR)
  │                              │                              │
  │  POST /auth/register         │                              │
  │ ────────────────────────────>│                              │
  │                              │──── RegisterCommand ────────>│
  │                              │                              │── IPasswordHasher.Hash()
  │                              │                              │── ITotpService.GenerateSecret()
  │                              │                              │── IEncryptionService.Encrypt(secret)
  │                              │                              │── IUserRepository.AddAsync()
  │  201 { userId, totpSecret, qrUri }                         │
  │ <────────────────────────────│                              │
  │                              │                              │
  │  POST /auth/login            │                              │
  │ ────────────────────────────>│                              │
  │                              │──── LoginCommand ───────────>│
  │                              │                              │── IUserRepository.GetByEmailAsync()
  │                              │                              │── IPasswordHasher.Verify()
  │                              │                              │── ITokenService.IssuePartialToken()
  │  200 { partialToken }        │                              │
  │ <────────────────────────────│                              │
  │                              │                              │
  │  POST /auth/mfa/verify       │                              │
  │  Authorization: Bearer <partial>                            │
  │ ────────────────────────────>│                              │
  │                              │──── VerifyMfaCommand ───────>│
  │                              │                              │── Extrai userId do partial token
  │                              │                              │── IEncryptionService.Decrypt(secret)
  │                              │                              │── ITotpService.Verify(secret, code)
  │                              │                              │── ITokenService.IssueAccessToken()
  │                              │                              │── ITokenService.GenerateRefreshToken()
  │  200 { accessToken, refreshToken }                         │
  │ <────────────────────────────│                              │
  │                              │                              │
  │  POST /auth/refresh          │                              │
  │ ────────────────────────────>│                              │
  │                              │──── RefreshCommand ─────────>│
  │                              │                              │── Valida refresh token no banco
  │                              │                              │── ITokenService.IssueAccessToken()
  │                              │                              │── ITokenService.GenerateRefreshToken()
  │  200 { accessToken, refreshToken }                         │
  │ <────────────────────────────│                              │
```

---

## 4. Regras de Negócio

### Registro (`POST /auth/register`)

1. E-mail deve ser único — retorna 422 se já existir (sem revelar se a conta existe: **Passo 4**)
2. Senha mínima: 8 caracteres (validação FluentValidation no `RegisterValidator`)
3. Segredo TOTP gerado automaticamente com `TotpService.GenerateSecret()` (20 bytes / 160 bits)
4. Segredo **cifrado** com AES-256-GCM antes de persistir em `User.TotpSecret`
5. O campo `IsMfaEnabled` é sempre `true` na criação — sem opção de desativar MFA
6. Retorna o segredo em texto claro UMA VEZ (`RegisterResult.TotpSecret`) para o usuário cadastrar no autenticador
7. Retorna o QR URI para ser codificado como QR code pelo front-end

### Login (`POST /auth/login`)

1. Busca o usuário pelo e-mail
2. Verifica o hash Argon2id com `IPasswordHasher.Verify()` (comparação em tempo constante)
3. Em caso de sucesso, emite um **partial token** (role=`mfa_pending`, TTL=5 min)
4. **Não** emite access token — o usuário precisa confirmar o TOTP
5. Resposta idêntica para e-mail inexistente e senha errada (previne enumeração de usuários)

### Verificação MFA (`POST /auth/mfa/verify`)

1. Endpoint requer `Authorization: Bearer <partial_token>` com role `mfa_pending`
2. `userId` vem do claim `sub` do partial token — **não** do corpo da requisição (Passo 3: corrigir `VerifyMfaCommand`)
3. Decifra o `TotpSecret` armazenado com `IEncryptionService.Decrypt()`
4. Verifica o código TOTP com janela de ±1 step (±30 s)
5. Em caso de sucesso, emite access token + refresh token

### Refresh (`POST /auth/refresh`)

1. Valida o refresh token contra o banco (implementado no Passo 3)
2. Rotaciona o refresh token (um token só pode ser usado uma vez)
3. Emite novo par access + refresh token
4. Refresh token expirado ou já utilizado retorna 401

---

## 5. Fluxo de Funcionamento (Sequência de Tokens)

```
Registro ──> [Configura autenticador com TotpSecret/QrUri]
                              │
Login ──────────────────> PartialToken (5 min, role=mfa_pending)
                              │
                    Verifica código TOTP
                              │
                     AccessToken (15 min) + RefreshToken (7 dias)
                              │
                  ┌───────────┴──────────────┐
            Request normal             Token expira
         (Bearer AccessToken)               │
                                    POST /auth/refresh
                                    ──> novo AccessToken + novo RefreshToken
```

---

## 6. Configurações

```json
"Auth": {
  "CertPath":                  "/certs/cert.pfx",
  "CertPassword":              "${CERT_PASSWORD}",
  "Issuer":                    "billing-engine",
  "AccessTokenExpiresMinutes": 15,
  "PartialTokenExpiresMinutes": 5,
  "RefreshTokenExpiresDays":   7
}
```

---

## 7. Cuidados de Segurança

| Risco                         | Mitigação                                                              |
|-------------------------------|------------------------------------------------------------------------|
| Enumeração de usuários        | Resposta idêntica para e-mail inexistente e senha errada               |
| Brute-force de senha          | Argon2id com 64 MB de memória — ~0.5 s por tentativa                  |
| Brute-force de TOTP           | Janela de 30 s + 6 dígitos = 1.000.000 combinações; rate limiting (Passo 4) |
| Replay de TOTP                | Proteção anti-replay via Redis (Passo 3)                               |
| Partial token reutilizado     | TTL de 5 min + role `mfa_pending` só aceita no endpoint de MFA        |
| Refresh token vazado          | Rotação obrigatória + armazenamento no banco (Passo 3)                |
| JWT falsificado               | Assinatura RS256 — chave privada nunca sai do servidor                |

---

## 8. Pendências (Próximos Passos)

- [ ] **Passo 3:** Implementar `RegisterHandler`, `LoginHandler`, `VerifyMfaHandler`, `RefreshHandler`
- [ ] **Passo 3:** Extrair `userId` do `sub` do partial token em `VerifyMfaHandler` (não do body)
- [ ] **Passo 3:** Armazenar refresh token no banco + rotação
- [ ] **Passo 3:** Endpoint `POST /auth/logout` (revogar refresh token)
- [ ] **Passo 4:** Rate limiting por IP e por usuário
- [ ] **Passo 4:** Lockout após 5 tentativas de login falhas

---

## 9. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                                      |
|------------|--------|----------------------|------------------------------------------------|
| 2026-10-08 | 1.0    | Equipe BillingEngine | Criação — fluxos documentados, handlers pendentes |
