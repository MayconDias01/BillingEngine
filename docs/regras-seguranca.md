# Regras de Segurança — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-08

---

## 1. Objetivo

Estabelecer as diretrizes e controles mínimos de segurança que governam o desenvolvimento,
a operação e a evolução do BillingEngine. Este documento é o ponto de entrada para os
documentos técnicos específicos de cada domínio de segurança.

---

## 2. Justificativa Técnica

O BillingEngine processa dados financeiros sensíveis (cobranças recorrentes, dados de
pagamento, documentos de clientes) e credenciais de usuários. Uma falha de segurança pode
resultar em:

- Exposição de dados de cartão ou documentos de clientes (LGPD / PCI-DSS)
- Account takeover (tomada de conta) por atacantes
- Cobranças fraudulentas ou duplicadas
- Responsabilidade legal e danos reputacionais

Por isso, segurança é requisito não-funcional de primeira classe — não um item de backlog.

---

## 3. Princípios Fundamentais

| Princípio               | Aplicação no projeto                                                              |
|-------------------------|-----------------------------------------------------------------------------------|
| **Defense in depth**    | Autenticação + autorização + criptografia em camadas independentes               |
| **Least privilege**     | Roles mínimas (`mfa_pending`, `user`); sem acesso a dados de outros usuários     |
| **Fail secure**         | Erros retornam 4xx/5xx genéricos; stack traces nunca chegam ao cliente           |
| **Zero trust**          | Todo token é validado a cada request; sem cache de autorização em memória        |
| **Criptografia por padrão** | Dados sensíveis (TOTP secret, documentos) sempre cifrados em repouso          |

---

## 4. Controles Implementados (Passo 1)

### 4.1 Hash de Senhas

- **Algoritmo:** Argon2id (OWASP #1, NIST SP 800-63B-4)
- **Parâmetros:** memória=64 MB, iterações=3, paralelismo=1
- **Detalhe:** [`docs/autenticacao.md`](autenticacao.md) | impl. [`Argon2PasswordHasher.cs`](../src/BillingEngine.Infrastructure/Security/Argon2PasswordHasher.cs)

### 4.2 Criptografia de Dados Sensíveis

- **Algoritmo:** AES-256-GCM (AEAD)
- **Uso:** segredo TOTP, documentos de clientes
- **Detalhe:** [`docs/criptografia.md`](criptografia.md) | impl. [`AesEncryptionService.cs`](../src/BillingEngine.Infrastructure/Security/AesEncryptionService.cs)

### 4.3 Tokens de Sessão

- **Algoritmo JWT:** RS256 (RSA 2048-bit + SHA-256)
- **Tipos:** partial token (mfa_pending / 5 min), access token (user / 15 min), refresh token (opaco / 7 dias)
- **Detalhe:** [`docs/jwt-rsa.md`](jwt-rsa.md) | impl. [`JwtTokenService.cs`](../src/BillingEngine.Infrastructure/Security/JwtTokenService.cs)

### 4.4 Autenticação Multifator

- **Protocolo:** TOTP RFC 6238 (HMAC-SHA1, 6 dígitos, 30 s)
- **Janela de tolerância:** ±1 step (±30 s)
- **Detalhe:** [`docs/mfa-totp.md`](mfa-totp.md) | impl. [`TotpService.cs`](../src/BillingEngine.Infrastructure/Security/TotpService.cs)

---

## 5. Controles Planejados (Passos 2–6)

| Controle                           | Passo | Status       |
|------------------------------------|-------|--------------|
| Repositórios com EF Core           | 2     | Pendente     |
| Armazenamento de refresh token     | 3     | Pendente     |
| Rate limiting por IP/usuário       | 4     | Pendente     |
| Proteção anti-replay do TOTP       | 3     | Pendente     |
| Lockout após N tentativas de login | 4     | Pendente     |
| Health checks com dependências     | 6     | Pendente     |
| OpenTelemetry + trilha de auditoria| 6     | Pendente     |
| RBAC granular / permissões         | 4     | Pendente     |
| Secrets via External Secrets Operator | 6  | Pendente     |

---

## 6. Política de Gestão de Segredos

### Desenvolvimento

```bash
# Gerar chave AES-256 (32 bytes)
openssl rand -base64 32

# Gerar certificado RSA auto-assinado para dev
openssl req -x509 -newkey rsa:2048 -keyout cert.key -out cert.crt -days 365 -nodes \
  -subj "/CN=billing-engine-dev"
openssl pkcs12 -export -out cert.pfx -inkey cert.key -in cert.crt \
  -passout pass:dev_password
```

### Produção

- Secrets NÃO devem estar em `appsettings.json` nem no repositório
- Usar variáveis de ambiente (Docker Compose / Kubernetes `secret`)
- Plano: migrar para External Secrets Operator (Vault / AWS Secrets Manager)

### Variáveis Obrigatórias

| Variável                 | Descrição                                           |
|--------------------------|-----------------------------------------------------|
| `Encryption__Key`        | Chave AES-256 em base64 (32 bytes)                  |
| `Auth__CertPath`         | Caminho para o arquivo .pfx com chave RSA           |
| `Auth__CertPassword`     | Senha do certificado .pfx                           |
| `ConnectionStrings__Postgres` | Connection string do PostgreSQL               |
| `Redis__ConnectionString`| Connection string do Redis                          |
| `RabbitMQ__Host`         | Host do RabbitMQ                                    |
| `RabbitMQ__Username`     | Usuário RabbitMQ                                    |
| `RabbitMQ__Password`     | Senha RabbitMQ                                      |

---

## 7. Referências Normativas

- [OWASP Top 10:2025](https://owasp.org/Top10/)
- [OWASP ASVS 5.0](https://owasp.org/www-project-application-security-verification-standard/)
- [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b.html) — Digital Identity Guidelines
- [NIST SP 800-38D](https://csrc.nist.gov/publications/detail/sp/800-38d/final) — AES-GCM
- [RFC 6238](https://datatracker.ietf.org/doc/html/rfc6238) — TOTP

---

## 8. Histórico de Alterações

| Data       | Versão | Autor            | Descrição                                              |
|------------|--------|------------------|--------------------------------------------------------|
| 2026-10-08 | 1.0    | Equipe BillingEngine | Criação inicial — Passo 1 (camada de segurança)  |
