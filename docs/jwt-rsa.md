# JWT com RSA (RS256) — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-08

---

## 1. Objetivo

Descrever a estratégia de emissão e validação de tokens JWT com assinatura assimétrica
RS256, os tipos de token utilizados e os procedimentos para gestão de chaves.

---

## 2. Justificativa Técnica

### Por que RS256 e não HS256?

| Critério                    | HS256 (HMAC)                     | RS256 (RSA) ✓                            |
|-----------------------------|----------------------------------|------------------------------------------|
| Tipo de chave               | Simétrica (segredo compartilhado)| Assimétrica (chave pública/privada)      |
| Distribuição                | Segredo precisa ser compartilhado| Chave pública pode ser publicada         |
| Validação em outros serviços| Precisa do segredo = risco       | Valida com chave pública = seguro        |
| Risco se vazado             | Qualquer um pode forjar tokens   | Apenas quem tem a chave privada assina   |
| Ideal para                  | Monólito simples                 | Microserviços / arquitetura distribuída  |

**ADR-002** (documentado no README): RS256 foi escolhido para permitir validação em
futuros microserviços sem distribuir o segredo de assinatura.

---

## 3. Arquitetura

```
                  ┌──────────────────────────────────────┐
                  │         JwtTokenService              │
                  │                                      │
                  │  ┌──────────────┐  ┌──────────────┐ │
                  │  │ signingKey   │  │ validationKey│ │
                  │  │ (chave priv.)│  │ (chave pública)│
                  │  └──────┬───────┘  └──────┬────────┘ │
                  └─────────┼─────────────────┼──────────┘
                            │                 │
              ┌─────────────┘                 └──────────────────┐
              ▼                                                   ▼
    IssuePartialToken()                          InfrastructureExtensions
    IssueAccessToken()                           AddJwtBearer(IssuerSigningKey)
    GenerateRefreshToken()                             │
              │                                        │
              ▼                                        ▼
    JWT assinado com RS256                  Middleware valida assinatura
    (retornado ao cliente)                  a cada request autenticado
```

---

## 4. Tipos de Token

### Partial Token

```json
// Header
{ "alg": "RS256", "typ": "JWT", "kid": "<thumbprint>" }

// Payload
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "role": "mfa_pending",
  "jti": "unique-id",
  "iat": 1728000000,
  "nbf": 1728000000,
  "exp": 1728000300   // +5 minutos
}
```

### Access Token

```json
// Payload
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "role": "user",
  "jti": "unique-id",
  "iat": 1728000300,
  "nbf": 1728000300,
  "exp": 1728001200   // +15 minutos
}
```

### Refresh Token

Bytes aleatórios (64 bytes / 512 bits) codificados em Base64. **Opaco** — não é JWT.
Armazenado no banco em Passo 3. Expiração configurável (padrão: 7 dias).

---

## 5. Regras de Negócio

1. Partial token tem role `mfa_pending` — aceito **apenas** no endpoint `/auth/mfa/verify`
2. Access token tem role `user` — aceito nos endpoints de negócio (`/subscriptions`, `/billing`)
3. `jti` (JWT ID) é único por token — base para blacklist de tokens revogados (Passo 6)
4. `ClockSkew` de 30 s compensa drift de relógio entre serviços
5. Chave privada carregada de `.pfx` em produção; chave efêmera em memória no desenvolvimento
6. A chave de validação (pública) é passada ao middleware JWT no startup — não recarregada por request

---

## 6. Configurações

```json
"Auth": {
  "CertPath":                   "/certs/cert.pfx",
  "CertPassword":               "${CERT_PASSWORD}",
  "Issuer":                     "billing-engine",
  "AccessTokenExpiresMinutes":  15,
  "PartialTokenExpiresMinutes": 5,
  "RefreshTokenExpiresDays":    7
}
```

---

## 7. Geração de Certificado

### Desenvolvimento (auto-assinado)

```bash
# Gerar chave privada RSA 2048-bit + certificado auto-assinado
openssl req -x509 -newkey rsa:2048 \
  -keyout cert.key -out cert.crt \
  -days 365 -nodes \
  -subj "/CN=billing-engine-dev/O=Dev/C=BR"

# Combinar em .pfx (PKCS#12)
openssl pkcs12 -export \
  -out cert.pfx \
  -inkey cert.key \
  -in cert.crt \
  -passout pass:dev_password

# Copiar para o local esperado
cp cert.pfx /certs/cert.pfx
# OU via docker-compose volume mount
```

### Produção

```bash
# Usar certificado emitido por CA confiável (Let's Encrypt, DigiCert, etc.)
# OU chave RSA pura gerada pelo KMS (AWS KMS, HashiCorp Vault)

# Exemplo com openssl (RSA 4096-bit para produção)
openssl genrsa -out prod.key 4096
openssl req -new -key prod.key -out prod.csr \
  -subj "/CN=billing-engine/O=MinhaEmpresa/C=BR"
# Assinar com CA da empresa ou enviar CSR para CA externa
```

### Docker Compose

```yaml
# docker-compose.yml — montar o certificado
api:
  volumes:
    - ./certs/cert.pfx:/certs/cert.pfx:ro
  environment:
    - Auth__CertPath=/certs/cert.pfx
    - Auth__CertPassword=${CERT_PASSWORD}
```

---

## 8. Comportamento de Fallback (Desenvolvimento)

Se `Auth:CertPath` não estiver configurado ou o arquivo não existir, `JwtTokenService`
gera uma chave RSA 2048-bit **efêmera em memória**:

- ✅ Tokens são emitidos e validados normalmente durante a sessão
- ❌ Todos os tokens são invalidados ao reiniciar a aplicação
- ❌ NÃO usar em produção — chave não persiste entre reinícios

Identificador da chave efêmera: `KeyId = "dev-ephemeral"`.

---

## 9. Cuidados de Segurança

| Risco                          | Mitigação                                                          |
|-------------------------------|--------------------------------------------------------------------|
| Chave privada exposta          | `signingKey` nunca sai de `JwtTokenService`; `GetValidationKey()` retorna apenas chave pública |
| Forja de token (algoritmo none)| `ValidateIssuerSigningKey = true` + `ValidateLifetime = true`      |
| Token de issuer diferente      | `ValidateIssuer = true` + `ValidIssuer` configurado               |
| Token expirado aceito          | `ValidateLifetime = true` + `ClockSkew = 30 s`                    |
| Rotação de chave sem downtime  | JWKS endpoint com múltiplas chaves ativas (Passo 6)               |

---

## 10. Limitações Conhecidas

1. **Sem JWKS endpoint:** serviços externos precisam receber a chave pública out-of-band
2. **Sem blacklist de tokens:** token roubado é válido até expirar (mitigação: TTL curto de 15 min)
3. **Chave única em produção:** sem rotação automática de chave; requer restart para trocar

---

## 11. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                                      |
|------------|--------|----------------------|------------------------------------------------|
| 2026-10-08 | 1.0    | Equipe BillingEngine | Criação — RS256 com fallback efêmero para dev  |
