# Autorização — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-08

---

## 1. Objetivo

Descrever o modelo de autorização do BillingEngine: os roles existentes, as políticas
aplicadas a cada endpoint e as regras de acesso a recursos entre usuários.

---

## 2. Justificativa Técnica

Autorização baseada em roles JWT é suficiente para o escopo atual (monólito com dois
processos). O modelo foi escolhido por ser stateless, verificável a cada request sem
consulta ao banco, e extensível para RBAC granular em etapas futuras.

---

## 3. Roles

| Role           | Atribuído quando                       | Endpoints acessíveis                     |
|----------------|----------------------------------------|------------------------------------------|
| `mfa_pending`  | Login bem-sucedido (antes do TOTP)     | `POST /auth/mfa/verify` apenas           |
| `user`         | MFA verificado (access token emitido)  | `/subscriptions/*`, `/billing/*`         |
| *(público)*    | Sem autenticação                       | `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `/health/*`, `/swagger/*` |

---

## 4. Políticas Configuradas

### Middleware (Program.cs)

```csharp
app.UseAuthentication();   // Valida o JWT Bearer
app.UseAuthorization();    // Aplica as políticas de role
```

### Endpoints

```csharp
// Exige role "mfa_pending"
group.MapPost("/mfa/verify", ...).RequireAuthorization(p => p.RequireRole("mfa_pending"));

// Exige role "user"
var subscriptionGroup = app.MapGroup("/subscriptions")
    .RequireAuthorization(p => p.RequireRole("user"));

var billingGroup = app.MapGroup("/billing")
    .RequireAuthorization(p => p.RequireRole("user"));
```

---

## 5. Ownership de Recursos (IDOR Prevention)

**Status: Pendente — Passo 3**

Atualmente, qualquer usuário autenticado com role `user` pode:
- Consultar qualquer assinatura por ID
- Disparar cobrança em qualquer assinatura
- Ver tentativas de cobrança de qualquer assinatura

Isso configura uma vulnerabilidade de IDOR (Insecure Direct Object Reference).

**Solução planejada (Passo 3):**

1. Adicionar `UserId` (FK) à entidade `Customer` ou `Subscription`
2. Em todo handler que recebe um ID de recurso, validar:
   ```csharp
   var userId = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
   var subscription = await _repo.GetByIdAsync(subscriptionId, ct);
   if (subscription?.OwnerId != userId) throw new ForbiddenException();
   ```
3. Retornar 403 Forbidden (não 404) para evitar confirmar existência de recurso alheio

---

## 6. Extração de Claims do Token

```csharp
// Obter userId do token em um handler/endpoint
var userId = Guid.Parse(HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
// ou via claim sub:
var userId = Guid.Parse(HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
```

---

## 7. Cuidados de Segurança

| Risco                          | Mitigação                                                             |
|-------------------------------|------------------------------------------------------------------------|
| Acesso a recurso alheio (IDOR) | Validação de ownership em todos os handlers (Passo 3)                |
| Escalada de privilege          | Roles atribuídas pelo servidor — cliente nunca define o próprio role  |
| Token de escopo incorreto      | `mfa_pending` não dá acesso a endpoints de dados; `user` não acessa MFA |
| Token expirado aceito           | `ValidateLifetime = true` + `ClockSkew = 30s` em `JwtTokenService`   |

---

## 8. Pendências

- [ ] **Passo 3:** Implementar checagem de ownership em `GetSubscriptionHandler`, `TriggerBillingHandler`, `GetBillingAttemptsHandler`
- [ ] **Passo 4:** RBAC granular (ex: `admin` role para gestão de planos)
- [ ] **Passo 4:** Auditoria de acessos negados (log estruturado)

---

## 9. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                              |
|------------|--------|----------------------|----------------------------------------|
| 2026-10-08 | 1.0    | Equipe BillingEngine | Criação — roles e políticas iniciais   |
