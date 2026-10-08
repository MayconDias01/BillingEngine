# BillingEngine

Motor de cobranca recorrente — prova de conceito de portfolio.

## Visao Geral

Dois processos: **BillingEngine.API** (Web API) e **BillingEngine.Worker** (Background Service).

Tres fluxos ponta a ponta:
1. **Auth** — Register, Login, MFA Verify, Refresh
2. **Subscriptions** — CreateSubscription, GetSubscription
3. **Billing** — TriggerBilling, GetBillingAttempts

## Arquitetura

```
BillingEngine.Domain           (entidades, value objects, enums, interfaces)
BillingEngine.Application      (CQRS com MediatR, validacao com FluentValidation)
BillingEngine.Infrastructure   (EF Core + PostgreSQL, Redis, RabbitMQ, JWT, Argon2, AES-256-GCM)
BillingEngine.API              (Minimal APIs, Swagger, JWT Bearer)
BillingEngine.Worker           (MassTransit consumer, BackgroundService)
```

## Como rodar

```bash
cp .env.example .env
# edite o .env com valores reais
docker-compose up --build
```

A API estara disponivel em `http://localhost:8080/swagger`.

## Endpoints

| Metodo | Rota                                | Auth         | Descricao                     |
|--------|-------------------------------------|--------------|-------------------------------|
| POST   | `/auth/register`                    | -            | Registrar usuario + MFA setup |
| POST   | `/auth/login`                       | -            | Login (retorna partial token) |
| POST   | `/auth/mfa/verify`                  | mfa_pending  | Verifica TOTP, retorna tokens |
| POST   | `/auth/refresh`                     | -            | Refresh token pair            |
| POST   | `/subscriptions`                    | user         | Criar subscription            |
| GET    | `/subscriptions/{id}`               | user         | Consultar subscription        |
| POST   | `/billing/charge/{subscriptionId}`  | user         | Disparar cobranca             |
| GET    | `/billing/{subscriptionId}/attempts`| user         | Listar tentativas             |

## Decisoes Tecnicas

### ADR-001: Argon2id vs BCrypt

**Decisao:** Argon2id.
**Motivo:** Resistente a ataques GPU e side-channel. Recomendado pelo OWASP como primeira opcao. BCrypt eh aceitavel, mas Argon2id oferece parametrizacao de memoria que dificulta ataques em hardware especializado.

### ADR-002: RSA vs HMAC para JWT

**Decisao:** RSA (RS256).
**Motivo:** Permite que servicos validem tokens sem conhecer a chave privada. Em arquitetura de microsservicos, a chave publica pode ser distribuida livremente. HMAC exigiria compartilhar o segredo com todos os servicos.

### ADR-003: AES-256-GCM vs AES-CBC

**Decisao:** AES-256-GCM.
**Motivo:** GCM fornece autenticacao integrada (AEAD), prevenindo ataques de padding oracle que afetam CBC. Tambem eh mais performatico em hardware moderno com instrucoes AES-NI.

## Roadmap

- [ ] Implementar logica de negocio nos Handlers
- [ ] Migrations do Entity Framework
- [ ] Retry policy com Polly para gateways de pagamento
- [ ] Integracoes reais com Stripe e PagarMe
- [ ] Observabilidade com OpenTelemetry
- [ ] Rate limiting na API
- [ ] External Secrets Operator para K8s
- [ ] Testes de integracao com Testcontainers
