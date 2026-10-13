# Persistência — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-12

---

## 1. Objetivo

Documentar a camada de persistência do BillingEngine: repositórios EF Core,
strategy de migrations, configurações de entidades e decisões de design.

---

## 2. Justificativa Técnica

### Por que Repository Pattern com EF Core?

- **Desacoplamento:** handlers da camada Application dependem de interfaces (`IUserRepository`)
  e não do EF Core diretamente — facilita testes unitários com mocks
- **EF Core:** ORM maduro com suporte a PostgreSQL, migrations automáticas e LINQ tipado
- **PostgreSQL:** banco relacional robusto, open-source, com suporte nativo a UUID, JSONB e índices parciais

---

## 3. Arquitetura

```
Application (Commands/Queries)
       │ usa interfaces
       ▼
Domain.Interfaces (IUserRepository, ISubscriptionRepository, ...)
       ▲ implementadas por
Infrastructure.Persistence.Repositories
       │ recebe por DI
       ▼
AppDbContext  ──────────────────────────────────── PostgreSQL
  │
  ├── Users
  ├── RefreshTokens
  ├── Customers
  ├── Plans
  ├── Subscriptions
  ├── Billings
  ├── BillingAttempts
  └── PaymentMethods
```

---

## 4. Repositórios

### UserRepository

| Método | SQL gerado (aproximado) |
|---|---|
| `GetByEmailAsync(email)` | `SELECT * FROM "Users" WHERE "Email" = @email LIMIT 1` |
| `AddAsync(user)` | `INSERT INTO "Users" (...)` + `SaveChangesAsync` |

### CustomerRepository

| Método | SQL gerado (aproximado) |
|---|---|
| `GetByIdAsync(id)` | `SELECT * FROM "Customers" WHERE "Id" = @id` (FindAsync — usa cache L1) |
| `AddAsync(customer)` | `INSERT INTO "Customers" (...)` + `SaveChangesAsync` |

### SubscriptionRepository

| Método | SQL gerado (aproximado) |
|---|---|
| `GetByIdAsync(id)` | `SELECT * FROM "Subscriptions" WHERE "Id" = @id` (FindAsync) |
| `AddAsync(subscription)` | `INSERT INTO "Subscriptions" (...)` |
| `UpdateAsync(subscription)` | `UPDATE "Subscriptions" SET ... WHERE "Id" = @id` |
| `GetDueForBillingAsync(asOf)` | `SELECT * FROM "Subscriptions" WHERE "Status" = 0 AND "NextBillingDate" <= @asOf` |

### RefreshTokenRepository

| Método | SQL gerado (aproximado) |
|---|---|
| `GetByTokenHashAsync(hash)` | `SELECT * FROM "RefreshTokens" WHERE "TokenHash" = @hash LIMIT 1` |
| `AddAsync(token)` | `INSERT INTO "RefreshTokens" (...)` + `SaveChangesAsync` |
| `UpdateAsync(token)` | `UPDATE "RefreshTokens" SET ... WHERE "Id" = @id` |

### BillingRepository

| Método | SQL gerado (aproximado) |
|---|---|
| `GetByIdAsync(id)` | `SELECT * FROM "Billings" WHERE "Id" = @id` (FindAsync) |
| `GetByIdempotencyKeyAsync(key)` | `SELECT * FROM "Billings" WHERE "IdempotencyKey" = @key LIMIT 1` |
| `AddAsync(billing)` | `INSERT INTO "Billings" (...)` |
| `UpdateAsync(billing)` | `UPDATE "Billings" SET ... WHERE "Id" = @id` |
| `GetAttemptsByBillingIdAsync(billingId)` | `SELECT * FROM "BillingAttempts" WHERE "BillingId" = @id ORDER BY "AttemptNumber"` |
| `AddAttemptAsync(attempt)` | `INSERT INTO "BillingAttempts" (...)` |
| `UpdateAttemptAsync(attempt)` | `UPDATE "BillingAttempts" SET ... WHERE "Id" = @id` |

---

## 5. Migrations

### Estratégia

- Migrations geradas pelo EF Core CLI (`dotnet ef migrations add`)
- Aplicadas automaticamente na inicialização da API (`db.Database.MigrateAsync()`)
- Arquivos de migration armazenados em `src/BillingEngine.Infrastructure/Persistence/Migrations/`

### Aplicar migrations manualmente

```bash
# Gerar nova migration
dotnet ef migrations add <NomeDaMigration> \
  --project src/BillingEngine.Infrastructure \
  --startup-project src/BillingEngine.API \
  --output-dir Persistence/Migrations

# Aplicar no banco (sem subir a aplicação)
dotnet ef database update \
  --project src/BillingEngine.Infrastructure \
  --startup-project src/BillingEngine.API

# Reverter para uma migration específica
dotnet ef database update <NomeDaMigration> \
  --project src/BillingEngine.Infrastructure \
  --startup-project src/BillingEngine.API

# Remover a última migration (se ainda não aplicada)
dotnet ef migrations remove \
  --project src/BillingEngine.Infrastructure \
  --startup-project src/BillingEngine.API
```

### Migrations criadas

| Migration | Data | Descrição |
|---|---|---|
| `InitialCreate` | 2026-10-12 | Criação de todas as tabelas: Users, Customers, Plans, Subscriptions, Billings, BillingAttempts, PaymentMethods |
| `AddRefreshTokens` | 2026-10-12 | Adiciona tabela `RefreshTokens` com índice único em `TokenHash` e índice composto em `(UserId, IsRevoked)` |

### Auto-migrate na inicialização (`Program.cs`)

```csharp
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

> **⚠️ Produção com múltiplas instâncias:** o `MigrateAsync` é idempotente, mas em
> ambientes com múltiplas réplicas pode causar race conditions durante o deploy.
> Para esses cenários, use um Kubernetes Job ou comando dedicado de migração.

---

## 6. Configuração de Entidades (EF Fluent API)

| Entidade | Configuração relevante |
|---|---|
| `User` | Índice único em `Email`; `PasswordHash` e `TotpSecret` obrigatórios |
| `RefreshToken` | Índice único em `TokenHash` (SHA-256 base64); índice composto `(UserId, IsRevoked)` |
| `Customer` | Índice em `Email`; `EncryptedDocument` é `text` (blob AES-GCM cifrado) |
| `Plan` | `BillingCycle` como owned type (valor objeto inline) |
| `Billing` | Índice único em `IdempotencyKey`; `Amount` com precisão (18,2) |
| `BillingAttempt` | Campos nullable para `GatewayTransactionId`, `FailureCode`, `FailureReason` |
| `Subscription` | Índice composto em `(CustomerId, PlanId, Status)` |

---

## 7. Design-Time Factory

`AppDbContextFactory` implementa `IDesignTimeDbContextFactory<AppDbContext>` e é usada
exclusivamente pelo EF CLI para geração de migrations — nunca em runtime.

```csharp
// Usada automaticamente pelo "dotnet ef migrations add"
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Connection string de design-time hardcoded — apenas para tooling
        ...
    }
}
```

---

## 8. Limitação: Ausência de Unit of Work

Os repositórios chamam `SaveChangesAsync` individualmente. Isso funciona para operações
simples, mas não garante atomicidade quando um handler precisa operar em múltiplos
agregados na mesma transação.

**Exemplo problemático (Passo 3):**
```
// Se AddAsync(billing) salva com sucesso mas AddAttemptAsync(attempt) falha,
// o banco fica em estado inconsistente.
await _billingRepo.AddAsync(billing, ct);
await _billingRepo.AddAttemptAsync(attempt, ct); // sem transação!
```

**Solução planejada (Passo 5):** Injetar o `AppDbContext` diretamente nos handlers
que precisam de atomicidade e chamar `SaveChangesAsync` uma única vez ao final.

---

## 9. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                                                                   |
|------------|--------|----------------------|-----------------------------------------------------------------------------|
| 2026-10-12 | 1.0    | Equipe BillingEngine | Criação — repositórios implementados, migration inicial                     |
| 2026-10-12 | 1.1    | Equipe BillingEngine | Adicionados RefreshToken entity, IRefreshTokenRepository, RefreshTokenRepository, migration AddRefreshTokens |
