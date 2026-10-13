# Autenticação — BillingEngine

> **Status:** Ativo · **Versão:** 2.0 · **Última revisão:** 2026-10-12

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

O refresh token é armazenado apenas como hash SHA-256 no banco de dados. Mesmo que o banco
seja comprometido, tokens em texto claro não são recuperáveis, pois 64 bytes aleatórios não
são suscetíveis a ataque de dicionário.

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
  │                              │                              │── ITotpService.GetQrUri()
  │                              │                              │── IEncryptionService.Encrypt(secret)
  │                              │                              │── IUserRepository.AddAsync()
  │  201 { userId, totpSecret, qrUri }                         │
  │ <────────────────────────────│                              │
  │                              │                              │
  │  POST /auth/login            │                              │
  │ ────────────────────────────>│                              │
  │                              │──── LoginCommand ───────────>│
  │                              │                              │── IUserRepository.GetByEmailAsync()
  │                              │                              │── IPasswordHasher.Verify() ← sempre executa
  │                              │                              │── ITokenService.IssuePartialToken()
  │  200 { partialToken }        │                              │
  │ <────────────────────────────│                              │
  │                              │                              │
  │  POST /auth/mfa/verify       │                              │
  │  Authorization: Bearer <partial>                            │
  │  Body: { code }              │                              │
  │ ────────────────────────────>│                              │
  │                              │── extrai userId do sub claim │
  │                              │──── VerifyMfaCommand ───────>│
  │                              │                              │── IUserRepository.GetByIdAsync()
  │                              │                              │── IEncryptionService.Decrypt()
  │                              │                              │── ITotpService.Verify()
  │                              │                              │── ITokenService.IssueAccessToken()
  │                              │                              │── ITokenService.GenerateRefreshToken()
  │                              │                              │── SHA256(refreshToken) → armazenar
  │                              │                              │── IRefreshTokenRepository.AddAsync()
  │  200 { accessToken, refreshToken }                         │
  │ <────────────────────────────│                              │
  │                              │                              │
  │  POST /auth/refresh          │                              │
  │  Body: { refreshToken }      │                              │
  │ ────────────────────────────>│                              │
  │                              │──── RefreshCommand ─────────>│
  │                              │                              │── SHA256(refreshToken) → buscar
  │                              │                              │── Valida IsValid() (não revogado + não expirado)
  │                              │                              │── Revoga token antigo (Revoke())
  │                              │                              │── ITokenService.IssueAccessToken()
  │                              │                              │── ITokenService.GenerateRefreshToken()
  │                              │                              │── SHA256(newToken) → armazenar
  │  200 { accessToken, refreshToken }                         │
  │ <────────────────────────────│                              │
```

---

## 4. Regras de Negócio

### Registro (`POST /auth/register`)

1. E-mail deve ser único — retorna 400 se já existir
2. Senha mínima: 8 caracteres (validação FluentValidation no `RegisterValidator`)
3. Segredo TOTP gerado automaticamente com `TotpService.GenerateSecret()` (20 bytes / 160 bits)
4. Segredo **cifrado** com AES-256-GCM antes de persistir em `User.TotpSecret`
5. O campo `IsMfaEnabled` é sempre `true` na criação — sem opção de desativar MFA
6. Retorna o segredo em texto claro **UMA VEZ** (`RegisterResult.TotpSecret`) para o usuário cadastrar no autenticador
7. Retorna o QR URI para ser codificado como QR code pelo front-end

### Login (`POST /auth/login`)

1. Busca o usuário pelo e-mail
2. **Sempre** executa `IPasswordHasher.Verify()` — mesmo para e-mails desconhecidos (usando hash dummy) — prevenindo enumeração de usuários por timing
3. Em caso de sucesso, emite um **partial token** (role=`mfa_pending`, TTL=5 min)
4. **Não** emite access token — o usuário precisa confirmar o TOTP
5. Resposta idêntica para e-mail inexistente e senha errada

### Verificação MFA (`POST /auth/mfa/verify`)

1. Endpoint requer `Authorization: Bearer <partial_token>` com role `mfa_pending`
2. `userId` é extraído do claim `sub` do partial token pelo endpoint — **não** do corpo da requisição (proteção contra IDOR)
3. Decifra o `TotpSecret` com `IEncryptionService.Decrypt()`
4. Verifica o código TOTP com janela de ±1 step (±30 s)
5. Em caso de sucesso, emite access token + refresh token
6. O hash SHA-256 do refresh token é armazenado em `RefreshTokens` com TTL de 7 dias

### Refresh (`POST /auth/refresh`)

1. Computa SHA-256 do token recebido e busca no banco
2. Valida: não revogado e não expirado
3. **Rotaciona**: revoga o token antigo e cria um novo par
4. Um token já utilizado ou expirado retorna 400

---

## 5. Fluxo de Funcionamento (Sequência de Tokens)

```
Registro ──> [Configura autenticador com TotpSecret/QrUri]
                          │
Login ──────────────> PartialToken (5 min, role=mfa_pending)
                          │
                Verifica código TOTP
                          │
               AccessToken (15 min) + RefreshToken (7 dias, hash no BD)
                          │
           ┌──────────────┴──────────────┐
     Request normal                Token expira
  (Bearer AccessToken)                  │
                               POST /auth/refresh
                           ──> novo AccessToken + novo RefreshToken
                               (token antigo revogado)
```

---

## 6. Entidade `RefreshToken`

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK para Users |
| `TokenHash` | `varchar(44)` | SHA-256 do token em base64 (44 chars) |
| `ExpiresAt` | `timestamptz` | Data de expiração |
| `IsRevoked` | `bool` | True se o token foi revogado |
| `CreatedAt` | `timestamptz` | Data de criação |
| `RevokedAt` | `timestamptz?` | Data de revogação (nullable) |

Índices:
- Único em `TokenHash` (lookup O(1))
- Composto em `(UserId, IsRevoked)` (consultas de auditoria)

---

## 7. Configurações

```json
"Auth": {
  "CertPath":                    "/certs/cert.pfx",
  "CertPassword":                "${CERT_PASSWORD}",
  "Issuer":                      "billing-engine",
  "AccessTokenExpiresMinutes":   15,
  "PartialTokenExpiresMinutes":  5,
  "RefreshTokenExpiresDays":     7
}
```

> `RefreshTokenExpiresDays` está hardcoded como `7` nos handlers.
> Para torná-lo configurável: criar `AuthOptions` e usar `IOptions<AuthOptions>`.

---

## 8. Cuidados de Segurança

| Risco                         | Mitigação                                                                      |
|-------------------------------|--------------------------------------------------------------------------------|
| Enumeração de usuários (login) | Hash Argon2id sempre executado — dummy hash para e-mails desconhecidos         |
| Brute-force de senha          | Argon2id com 64 MB de memória — ~0,5 s por tentativa                          |
| Brute-force de TOTP           | Janela de 30 s + 6 dígitos = 1.000.000 combinações; rate limiting (Passo 4)   |
| Replay de TOTP                | Proteção anti-replay via Redis (Passo 5)                                       |
| IDOR no `/mfa/verify`         | `userId` extraído do JWT claim `sub` — não do body                             |
| Partial token reutilizado     | TTL de 5 min + role `mfa_pending` só aceita no endpoint de MFA                 |
| Refresh token vazado          | Hash SHA-256 armazenado — token em claro nunca persistido                      |
| Refresh token reutilizado     | Rotação obrigatória: token revogado na primeira utilização                     |
| JWT falsificado               | Assinatura RS256 — chave privada nunca sai do servidor                         |

---

## 9. Limitações Conhecidas

- `RefreshTokenExpiresDays` está hardcoded nos handlers (não configurável via `appsettings.json`)
- Não há proteção anti-replay de TOTP (Redis pendente — Passo 5)
- Não há endpoint `POST /auth/logout` (revogação explícita do refresh token)
- Não há lockout após tentativas de login falhas (Passo 4)

---

## 10. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                                                                  |
|------------|--------|----------------------|----------------------------------------------------------------------------|
| 2026-10-08 | 1.0    | Equipe BillingEngine | Criação — fluxos documentados, handlers pendentes                          |
| 2026-10-12 | 2.0    | Equipe BillingEngine | Handlers implementados: Register, Login, VerifyMfa, Refresh; RefreshToken entity; migration AddRefreshTokens; endpoint MFA corrigido (IDOR) |
