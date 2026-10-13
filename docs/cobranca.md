# Fluxo de Cobrança — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-12

---

## 1. Objetivo

Documentar o fluxo de cobrança recorrente do BillingEngine: como uma cobrança é disparada,
processada de forma assíncrona, protegida contra duplicatas (idempotência) e como o resultado
é persistido.

---

## 2. Justificativa Técnica

### Por que RabbitMQ + Worker em vez de processamento síncrono?

- **Resiliência**: O gateway de pagamento pode estar lento ou indisponível. Publicar uma mensagem
  é instantâneo; o processamento acontece quando possível.
- **Desacoplamento**: A API retorna `202 Accepted` imediatamente — o usuário não espera o gateway.
- **Retry automático**: MassTransit gerencia filas de retry para falhas transientes.
- **Escalabilidade**: Múltiplas instâncias do Worker podem processar cobranças em paralelo.

### Por que idempotência em dois níveis?

| Nível | Onde | Chave | Propósito |
|---|---|---|---|
| **Banco** | `Billings.IdempotencyKey` | `billing:{subId}:{yyyyMMdd}` | Evita criar dois `Billing` para o mesmo dia |
| **Redis** | `billing-processed:{key}` | Mesma chave | Evita processar a mesma mensagem duas vezes (at-most-once no consumer) |

---

## 3. Arquitetura

```
Cliente          API                   RabbitMQ        Worker               PostgreSQL
  │               │                      │                │                    │
  │ POST /billing  │                      │                │                    │
  │ /charge/{id}  │                      │                │                    │
  │──────────────>│                      │                │                    │
  │               │── TriggerBillingHandler              │                    │
  │               │    ├── Verifica IDOR (customerId)    │                    │
  │               │    ├── Verifica subscription.Active  │                    │
  │               │    ├── Checa idempotência no BD      │────────────────────>│
  │               │    ├── Cria Billing (Pending)        │────────────────────>│
  │               │    └── Publica BillingRequested ─────>│                    │
  │ 202 Accepted  │                      │                │                    │
  │<──────────────│                      │                │                    │
  │               │                      │                │                    │
  │               │                      │ BillingRequested                    │
  │               │                      │──────────────->│                    │
  │               │                      │                │── Checa Redis       │
  │               │                      │                │   (SetIfNotExists) │
  │               │                      │                │── Busca Billing ───>│
  │               │                      │                │── Busca Subscription>│
  │               │                      │                │── Busca Plan ───────>│
  │               │                      │                │── Cria Attempt ─────>│
  │               │                      │                │── ChargeAsync()     │
  │               │                      │                │── Atualiza Attempt──>│
  │               │                      │                │── Atualiza Billing──>│
  │               │                      │                │── Atualiza NextDate─>│
```

### Scanner Loop (Worker)

```
Worker.ExecuteAsync()
  └── a cada 1 minuto:
       ├── GetDueForBillingAsync(DateTime.UtcNow)
       └── para cada assinatura vencida:
            └── TriggerBillingCommand(subscriptionId, RequestingUserId=null)
                 └── TriggerBillingHandler (sem IDOR check — contexto de sistema)
```

---

## 4. Regras de Negócio

### `POST /billing/charge/{subscriptionId}` (API)

1. Requer autenticação (role `user`)
2. `userId` extraído do JWT — IDOR: apenas o dono da assinatura pode disparar cobrança
3. Assinatura deve estar com status `Active`
4. **Idempotência por dia**: chave `billing:{subscriptionId}:{yyyyMMdd}` — apenas uma cobrança por dia
5. Se billing com essa chave já existir: retorna `202 Accepted` silenciosamente (idempotente)
6. Cria `Billing` com status `Pending` e publica `BillingRequested`

### `BillingRequestedConsumer` (Worker)

1. **Redis** `SetIfNotExistsAsync("billing-processed:{key}", TTL=24h)`:
   - Se retornar `false` (já existe): mensagem duplicada → skip
   - Se retornar `true`: processar
2. Carrega `Billing`, `Subscription` e `Plan` do banco
3. Cria `BillingAttempt` com status `Processing`
4. Chama `IPaymentGateway.ChargeAsync()`
5. **Sucesso**: `Attempt.Complete()`, `Billing.MarkAsPaid()`, avança `NextBillingDate`
6. **Falha**: `Attempt.Fail(code, reason)`, `Billing.MarkAsFailed()`
7. `NextBillingDate` é avançado por `plan.BillingCycle.IntervalDays` a partir da **data atual** da assinatura (não de UtcNow), evitando drift de ciclo

### Scanner Loop

1. Roda a cada 60 segundos
2. Busca todas as assinaturas `Active` com `NextBillingDate <= DateTime.UtcNow`
3. Para cada uma, dispara `TriggerBillingCommand` sem `RequestingUserId` (contexto de sistema)
4. Erros por assinatura são logados mas não param o loop

---

## 5. Idempotência em detalhe

```
Cenário: mensagem processada duas vezes (retry de RabbitMQ)

1ª entrega:
  SetIfNotExistsAsync("billing-processed:billing:sub-1:20261012", 24h) → true (nova)
  → processa normalmente

2ª entrega (retry):
  SetIfNotExistsAsync("billing-processed:billing:sub-1:20261012", 24h) → false (já existe)
  → log "already processed" → return (sem cobrança dupla)
```

```
Cenário: API chamada duas vezes no mesmo dia

1ª chamada:
  GetByIdempotencyKeyAsync("billing:sub-1:20261012") → null
  → cria Billing → publica BillingRequested

2ª chamada:
  GetByIdempotencyKeyAsync("billing:sub-1:20261012") → Billing existente
  → retorna 202 sem criar novo Billing
```

---

## 6. Configuração

```json
"ConnectionStrings": {
  "Postgres": "Host=...;Database=billing;..."
},
"RabbitMQ": {
  "Host":     "localhost",
  "Username": "guest",
  "Password": "guest"
},
"Redis": {
  "ConnectionString": "localhost:6379"
}
```

### appsettings.json (Worker)

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=billing;Username=billing;Password=billing"
  },
  "RabbitMQ": {
    "Host":     "localhost",
    "Username": "guest",
    "Password": "guest"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

---

## 7. Cuidados de Segurança

| Risco | Mitigação |
|---|---|
| Double-charge (cobrança dupla) | Idempotência em dois níveis: BD + Redis |
| IDOR (disparo para assinatura alheia) | `TriggerBillingCommand.RequestingUserId` valida ownership |
| Retry storm do RabbitMQ | Idempotência no consumer — retries são no-ops |
| Gateway timeout | MassTransit retry policy (configurável) |
| Ciclo de billing desalinhado | `NextBillingDate` avança a partir da data anterior (não de UtcNow) |

---

## 8. Limitações Conhecidas

- `FakePaymentGateway` sempre retorna sucesso — gateway real pendente (Stripe/PagMe/Safe2Pay)
- Não há política de retry configurada no MassTransit (usa o default) — pendente Passo 6
- Não há monitoramento de falhas em série (suspensão automática de assinatura após N falhas) — pendente
- `ScanInterval` hardcoded em 1 minuto — não configurável via `appsettings.json`
- Sem dead-letter queue configurada para mensagens que falham após todos os retries

---

## 9. Histórico de Alterações

| Data       | Versão | Autor                | Descrição |
|------------|--------|----------------------|-----------|
| 2026-10-12 | 1.0    | Equipe BillingEngine | Criação — TriggerBillingHandler, BillingRequestedConsumer, Worker scanner loop, RabbitMqMessageBus, RedisIdempotencyService implementados |
