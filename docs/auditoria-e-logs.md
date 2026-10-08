# Auditoria e Logs — BillingEngine

> **Status:** Planejado (Passo 6) · **Versão:** 0.1 · **Última revisão:** 2026-10-08

---

## 1. Objetivo

Definir a estratégia de observabilidade, logs estruturados e trilha de auditoria do
BillingEngine, garantindo rastreabilidade de eventos de segurança e operacionais.

---

## 2. Justificativa Técnica

Sistemas de cobrança recorrente precisam de auditoria completa porque:

- **Regulatório:** LGPD exige registro de acesso a dados pessoais
- **Investigação de incidentes:** logs estruturados permitem correlação de eventos
- **Debugging de produção:** telemetria distribuída é essencial em arquitetura com Worker + API
- **Confiança do cliente:** histórico auditável de tentativas de cobrança

---

## 3. Arquitetura Planejada

```
API / Worker
     │
     ▼
Serilog (ILogger<T>)
     │
     ├── Console Sink (desenvolvimento)
     ├── File Sink / Seq Sink (staging)
     └── OpenTelemetry Exporter ──> Jaeger / Grafana Tempo (produção)
                                           │
                                    Prometheus (métricas)
                                    Loki (logs agregados)
```

---

## 4. Eventos de Auditoria (a implementar)

### Segurança

| Evento                        | Nível | Campos obrigatórios                                        |
|-------------------------------|-------|------------------------------------------------------------|
| `user.registered`             | Info  | userId, email (anonimizado), timestamp, ip                |
| `user.login.success`          | Info  | userId, timestamp, ip, userAgent                          |
| `user.login.failed`           | Warn  | email (anonimizado), reason, timestamp, ip                |
| `user.mfa.verified`           | Info  | userId, timestamp, ip                                     |
| `user.mfa.failed`             | Warn  | userId, attemptCount, timestamp, ip                       |
| `user.token.refreshed`        | Info  | userId, timestamp, ip                                     |
| `user.session.revoked`        | Info  | userId, reason, timestamp                                 |
| `auth.forbidden`              | Warn  | userId, resource, timestamp, ip                           |

### Operacional

| Evento                        | Nível | Campos obrigatórios                                        |
|-------------------------------|-------|------------------------------------------------------------|
| `billing.triggered`           | Info  | subscriptionId, idempotencyKey, timestamp                 |
| `billing.attempt.success`     | Info  | billingId, attemptId, transactionId, gateway, timestamp   |
| `billing.attempt.failed`      | Warn  | billingId, attemptId, failureCode, failureType, timestamp |
| `billing.duplicate.rejected`  | Info  | subscriptionId, idempotencyKey, timestamp                 |
| `subscription.created`        | Info  | subscriptionId, customerId, planId, timestamp             |
| `subscription.cancelled`      | Info  | subscriptionId, reason, timestamp                         |

---

## 5. Regras de Negócio de Auditoria

1. **Nunca logar dados sensíveis:** senhas, tokens, segredos TOTP, números de cartão, documentos
2. **E-mail anonimizado em logs:** `us***@example.com` — apenas para correlação, nunca o e-mail completo
3. **Correlation ID:** toda requisição deve ter um `X-Correlation-Id` propagado pelo pipeline
4. **Retenção:** logs de segurança devem ser retidos por mínimo 12 meses (LGPD)
5. **Imutabilidade:** logs de auditoria não devem ser deletáveis por operadores comuns

---

## 6. Configuração Planejada

```csharp
// Program.cs (Passo 6)
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithCorrelationId()
        .Enrich.WithProperty("Application", "BillingEngine.API")
        .WriteTo.Console(new RenderedCompactJsonFormatter())
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = context.Configuration["Otel:Endpoint"];
        });
});
```

```json
// appsettings.json (Passo 6)
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.EntityFrameworkCore": "Warning",
      "MassTransit": "Warning"
    }
  }
}
```

---

## 7. Métricas Planejadas (OpenTelemetry)

| Métrica                            | Tipo      | Descrição                              |
|------------------------------------|-----------|----------------------------------------|
| `billing_attempts_total`           | Counter   | Total de tentativas de cobrança        |
| `billing_success_rate`             | Gauge     | Taxa de sucesso por gateway            |
| `auth_login_total`                 | Counter   | Total de logins (sucesso/falha)        |
| `mfa_verification_duration_ms`     | Histogram | Tempo de verificação TOTP              |
| `password_hash_duration_ms`        | Histogram | Tempo de hash Argon2id                 |

---

## 8. Pendências

- [ ] **Passo 6:** Instalar Serilog + OpenTelemetry
- [ ] **Passo 6:** Adicionar `Enrich.WithCorrelationId()` ao pipeline HTTP
- [ ] **Passo 6:** Logar eventos de autenticação nos handlers (Passo 3)
- [ ] **Passo 6:** Logar eventos de cobrança no Worker (Passo 5)
- [ ] **Passo 6:** Definir alertas (ex: taxa de falha de cobrança > 10%)
- [ ] **Passo 6:** Configurar exportação para Grafana / Jaeger

---

## 9. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                                      |
|------------|--------|----------------------|------------------------------------------------|
| 2026-10-08 | 0.1    | Equipe BillingEngine | Criação — especificação planejada (Passo 6)    |
