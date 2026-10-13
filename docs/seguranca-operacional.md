# Segurança Operacional — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-12

---

## 1. Rate Limiting

### Estratégia

| Política | Endpoints | Limite | Janela | Particionamento |
|---|---|---|---|---|
| `auth` | `POST /auth/login`, `POST /auth/register` | 5 requests | 1 minuto (sliding) | Por IP remoto |

**Algoritmo:** Sliding Window com 6 segmentos (10s cada).

**Por que Sliding Window?**
- Previne burst no exato instante do reset da janela (Fixed Window é vulnerável)
- 6 segmentos = granularidade de 10 segundos → menos picos de burst dentro da janela

**Por que por IP?**
- Isolamento: um atacante com 1 IP não consome a cota de toda a aplicação
- Limitação: IP não é garantia contra múltiplos IPs coordenados (botnet); para isso, use WAF

### Resposta ao exceder o limite

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 42
Content-Type: application/json

{
  "type": "RateLimitExceeded",
  "message": "Too many requests. Please try again later."
}
```

### Nota sobre reverse proxy

Quando a API fica atrás de um NGINX ou load balancer, `RemoteIpAddress` retorna o IP do proxy,
não do cliente real. Para usar o IP do cliente, configure o middleware `ForwardedHeaders` **antes**
do `UseRateLimiter()`:

```csharp
// Program.cs — adicionar ANTES de app.UseRateLimiter()
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

E configure o NGINX para incluir `proxy_set_header X-Forwarded-For $remote_addr;`.

---

## 2. External Secrets

### Estratégia adotada: External Secrets Operator (Kubernetes)

O BillingEngine **não lê segredos diretamente** de provedores de nuvem (AWS SM, Azure Key Vault).
Segue o padrão de separação de responsabilidades:

```
[AWS Secrets Manager / Azure Key Vault / HashiCorp Vault]
    ↓
[External Secrets Operator (K8s)]  ←── reconcilia periodicamente
    ↓
[K8s Secret]  ←── montado como env vars no Pod
    ↓
[BillingEngine Pod]  ←── lê via IConfiguration (env vars)
```

### Mapeamento de segredos

| Segredo | Chave no app (`IConfiguration`) | Nome sugerido no SM |
|---|---|---|
| Connection string PostgreSQL | `ConnectionStrings__Postgres` | `billing/postgres/connection-string` |
| Chave de criptografia AES | `Encryption__Key` | `billing/encryption-key` |
| Senha do certificado JWT | `Auth__CertPassword` | `billing/jwt/cert-password` |
| Redis connection string | `Redis__ConnectionString` | `billing/redis/connection-string` |
| Senha RabbitMQ | `RabbitMQ__Password` | `billing/rabbitmq/password` |

### ExternalSecret manifest (exemplo AWS)

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: billing-engine-secrets
  namespace: billing
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: aws-secrets-manager
    kind: ClusterSecretStore
  target:
    name: billing-engine-env
    creationPolicy: Owner
  data:
    - secretKey: ConnectionStrings__Postgres
      remoteRef:
        key: billing/postgres/connection-string
    - secretKey: Encryption__Key
      remoteRef:
        key: billing/encryption-key
    - secretKey: Auth__CertPassword
      remoteRef:
        key: billing/jwt/cert-password
    - secretKey: Redis__ConnectionString
      remoteRef:
        key: billing/redis/connection-string
    - secretKey: RabbitMQ__Password
      remoteRef:
        key: billing/rabbitmq/password
```

### Alternativa: SDK direto

Para injetar segredos diretamente no `IConfigurationBuilder` sem ESO, adicione o pacote
`Amazon.Extensions.Configuration.SystemsManager` (AWS) ou
`Azure.Extensions.AspNetCore.Configuration.Secrets` (Azure):

```csharp
// Program.cs — antes de builder.Build()
if (builder.Configuration["ExternalSecrets:Provider"] == "aws")
{
    builder.Configuration.AddSystemsManager(
        path: $"/billing/{builder.Environment.EnvironmentName}",
        optional: false,
        reloadAfter: TimeSpan.FromMinutes(15));
}
```

O BillingEngine **não inclui esse pacote por padrão** — use a abordagem ESO em produção com K8s,
ou variáveis de ambiente em ambientes sem K8s.

---

## 3. Práticas de segurança em produção (checklist)

- [ ] HTTPS obrigatório — configure no ingress (NGINX/Traefik); NÃO no app .NET em produção
- [ ] `appsettings.json` não contém segredos reais — todos os campos sensíveis ficam em branco ou em env vars
- [ ] Certificado JWT (`.p12`) em K8s Secret, montado como volume read-only
- [ ] Headers de segurança (`X-Content-Type-Options`, `X-Frame-Options`, `Strict-Transport-Security`) — configure no ingress ou com middleware `NWebsec`
- [ ] CORS — por padrão não configurado; adicionar antes de go-live com política restritiva
- [ ] Audit log de operações sensíveis (login, cobrança) — pendente
- [ ] Dependências atualizadas — executar `dotnet list package --vulnerable` no CI

---

## 4. Limitações Conhecidas

- `ForwardedHeaders` não está configurado — rate limiting usa `RemoteIpAddress` (IP do proxy em prod)
- Headers de segurança HTTP não implementados no middleware .NET — pendente configuração no ingress
- CORS não configurado
- Audit log não implementado

---

## 5. Histórico de Alterações

| Data       | Versão | Autor                | Descrição |
|------------|--------|----------------------|-----------|
| 2026-10-12 | 1.0    | Equipe BillingEngine | Criação — rate limiting (sliding window, por IP, 5 req/min nos endpoints de auth), padrão External Secrets (K8s ESO), checklist de produção |
