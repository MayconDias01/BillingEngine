# Assinaturas e Planos — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-12

---

## 1. Objetivo

Documentar os fluxos de criação e consulta de assinaturas, a estrutura de planos disponíveis,
as regras de negócio aplicadas e as proteções contra IDOR (Insecure Direct Object Reference).

---

## 2. Justificativa Técnica

### Por que Customer tem UserId?

Para garantir que um usuário autenticado não possa criar assinaturas para clientes de
outros usuários (IDOR), o `Customer` possui um campo `UserId` que indica qual usuário
criou e "possui" aquele cliente. Todas as operações sobre assinaturas verificam essa
relação antes de agir.

### Por que NextBillingDate é derivado do BillingCycle do plano?

O `Subscription.Create()` da entidade de domínio calcula a data de primeiro faturamento
com base nos dias de intervalo definidos no `Plan.BillingCycle.IntervalDays`. Isso garante
que o ciclo de cobrança seja sempre consistente com o plano contratado.

---

## 3. Arquitetura

```
Cliente                    API (Minimal API)            Application (MediatR)
  │                              │                              │
  │  GET /plans                  │                              │
  │ ────────────────────────────>│                              │
  │                              │──── GetAllPlansQuery ───────>│
  │                              │                              │── IPlanRepository.GetAllActiveAsync()
  │  200 [{ id, name, price, currency, intervalDays }]         │
  │ <────────────────────────────│                              │
  │                              │                              │
  │  POST /subscriptions         │                              │
  │  Authorization: Bearer <tok> │                              │
  │  Body: { customerId, planId, │                              │
  │          gateway, ... }      │                              │
  │ ────────────────────────────>│                              │
  │                              │── extrai userId do JWT sub   │
  │                              │──── CreateSubscriptionCommand>│
  │                              │                              │── CustomerRepository.GetByIdForUserAsync()
  │                              │                              │   (IDOR: customer.UserId == requestingUserId)
  │                              │                              │── PlanRepository.GetByIdAsync()
  │                              │                              │   (valida plan.IsActive)
  │                              │                              │── Subscription.Create(...)
  │                              │                              │── UpdateNextBillingDate(UtcNow + IntervalDays)
  │                              │                              │── SubscriptionRepository.AddAsync()
  │  201 { id }                  │                              │
  │ <────────────────────────────│                              │
  │                              │                              │
  │  GET /subscriptions/{id}     │                              │
  │  Authorization: Bearer <tok> │                              │
  │ ────────────────────────────>│                              │
  │                              │── extrai userId do JWT sub   │
  │                              │──── GetSubscriptionQuery ───>│
  │                              │                              │── SubscriptionRepository.GetByIdAsync()
  │                              │                              │── CustomerRepository.GetByIdForUserAsync()
  │                              │                              │   (IDOR: retorna null se não for o dono)
  │  200 SubscriptionDto         │                              │
  │  (ou 404 se não encontrado   │                              │
  │   ou não autorizado)         │                              │
  │ <────────────────────────────│                              │
```

---

## 4. Planos (Seed)

Três planos são inseridos automaticamente via migration `AddCustomerUserIdAndPlanSeed`:

| ID | Nome | Preço | Moeda | Ciclo |
|---|---|---|---|---|
| `10000000-0000-0000-0000-000000000001` | Basic | $9,99 | USD | 30 dias |
| `10000000-0000-0000-0000-000000000002` | Pro | $29,99 | USD | 30 dias |
| `10000000-0000-0000-0000-000000000003` | Enterprise | $299,99 | USD | 365 dias |

Os planos são imutáveis em runtime (sem endpoint de criação/edição por enquanto).
Para adicionar novos planos: criar uma nova migration com `InsertData`.

---

## 5. Regras de Negócio

### `GET /plans`

1. Retorna apenas planos com `IsActive = true`
2. Requer autenticação (role `user`)

### `POST /subscriptions`

1. Requer autenticação (role `user`)
2. `CustomerId` deve existir e pertencer ao usuário autenticado (proteção IDOR)
3. `PlanId` deve existir e estar ativo (`IsActive = true`)
4. `NextBillingDate = DateTime.UtcNow.AddDays(plan.BillingCycle.IntervalDays)`
5. Status inicial da assinatura: `Active`
6. Retorna 201 Created com `{ id: Guid }`
7. Retorna 400 Bad Request se o cliente não for encontrado ou o plano estiver inativo

### `GET /subscriptions/{id}`

1. Requer autenticação (role `user`)
2. Carrega a assinatura e verifica que o `CustomerId` da assinatura pertence ao usuário autenticado
3. Retorna **404 Not Found** em caso de assinatura inexistente **ou** não autorizada — evita revelar a existência do recurso (proteção IDOR)

---

## 6. Proteção IDOR

**Cenário de ataque sem proteção:**
```
Alice (userId=A) tem customer=C1, subscription=S1
Bob (userId=B) envia GET /subscriptions/S1
→ Sem proteção: Bob vê a assinatura de Alice
```

**Com proteção implementada:**
```
Handler verifica: customer.UserId == requestingUserId
Se não bater → retorna null → endpoint retorna 404
Bob não consegue confirmar se S1 existe
```

O padrão de retornar 404 (ao invés de 403) é deliberado: não revela se o recurso existe,
prevenindo reconnaissance.

---

## 7. Modelo de Dados

### `Customer`

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK para Users — indica o proprietário |
| `Name` | `varchar(256)` | Nome do cliente |
| `Email` | `varchar(256)` | E-mail do cliente |
| `EncryptedDocument` | `text` | CPF/CNPJ cifrado com AES-256-GCM |
| `CreatedAt` | `timestamptz` | Data de criação |

### `Plan`

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `Name` | `varchar(128)` | Nome do plano |
| `Price` | `numeric(18,2)` | Preço |
| `Currency` | `varchar(3)` | Moeda (ex: "USD") |
| `BillingCycle_IntervalDays` | `int` | Dias entre cobranças |
| `IsActive` | `bool` | Se o plano está disponível |

### `Subscription`

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | `Guid` | PK |
| `CustomerId` | `Guid` | FK para Customers |
| `PlanId` | `Guid` | FK para Plans |
| `Gateway` | `int` | Gateway de pagamento (enum) |
| `GatewayCustomerId` | `varchar(256)` | ID do cliente no gateway |
| `GatewayPaymentMethodId` | `varchar(256)` | ID do método de pagamento no gateway |
| `Status` | `int` | Active / Suspended / Cancelled |
| `NextBillingDate` | `timestamptz` | Próxima data de cobrança |
| `CreatedAt` | `timestamptz` | Data de criação |

---

## 8. Configuração

Nenhuma configuração adicional necessária. Os planos seed são inseridos via migration.

---

## 9. Cuidados de Segurança

| Risco | Mitigação |
|---|---|
| IDOR — acesso a assinatura de outro usuário | `GetByIdForUserAsync` filtra por `(Id, UserId)` |
| IDOR — criação de assinatura para cliente alheio | Handler valida `customer.UserId == requestingUserId` |
| Resposta discriminatória (403 vs 404) | Sempre retorna 404 para recursos não encontrados **ou** não autorizados |
| Plano inativo sendo assinado | Handler verifica `plan.IsActive` antes de criar |

---

## 10. Limitações Conhecidas

- Não há endpoint para criar/editar clientes (`POST /customers`) — pendente
- Não há endpoint para cancelar assinatura (`DELETE /subscriptions/{id}`) — pendente
- Não há listagem de assinaturas por usuário (`GET /subscriptions`) — pendente (Passo 4+)
- O relacionamento User→Customer é 1:N, mas não há validação de limite máximo de clientes por usuário

---

## 11. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                                              |
|------------|--------|----------------------|--------------------------------------------------------|
| 2026-10-12 | 1.0    | Equipe BillingEngine | Criação — CreateSubscriptionHandler, GetSubscriptionHandler, GetAllPlansHandler, planos seed, proteção IDOR |
