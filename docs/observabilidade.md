# Observabilidade — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-12

---

## 1. Objetivo

Documentar a estratégia de observabilidade do BillingEngine: traces distribuídos, métricas de
runtime, health checks de dependências e como integrar com coletores externos (Jaeger, Grafana
Tempo, Prometheus/OTLP).

---

## 2. Stack

| Componente | Tecnologia | Exportador |
|---|---|---|
| Traces | OpenTelemetry .NET SDK | OTLP (gRPC) |
| Métricas | OpenTelemetry .NET SDK | OTLP (gRPC) |
| Health Checks | ASP.NET Core Health Checks | HTTP JSON |
| Logs | Microsoft.Extensions.Logging | stdout (JSON em produção) |

---

## 3. OpenTelemetry

### 3.1 API (BillingEngine.API)

**Fontes ativas** (activity sources e meters built-in do .NET 9 — sem pacotes extras):

| Fonte | Tipo | O que captura |
|---|---|---|
| `"Microsoft.AspNetCore"` | Trace | Todas as requisições HTTP recebidas |
| `"System.Net.Http"` | Trace | Chamadas HTTP de saída |
| `"Microsoft.AspNetCore.Hosting"` | Metric | Contagem e latência de requests |
| `"Microsoft.AspNetCore.Server.Kestrel"` | Metric | Conexões ativas, throughput |
| `"System.Net.Http"` | Metric | Chamadas HTTP de saída |

**Sinais exportados:**
- **Traces**: spans com atributos `http.method`, `http.route`, `http.status_code`
- **Metrics**: contadores e histogramas de requests (`http.server.request.duration`, etc.)

**Exportador:** OTLP gRPC em `Otel:Endpoint` (default `http://localhost:4317`)

> **Por que não usar `OpenTelemetry.Instrumentation.AspNetCore`?**
> No .NET 8/9, o runtime já emite activities e meters nativamente. Os pacotes de instrumentação
> são necessários apenas para .NET 6/7 ou para atributos extras (route templates, etc.). A
> abordagem com `AddSource`/`AddMeter` é mais leve e não tem dependências de pacotes adicionais.

### 3.2 Worker (BillingEngine.Worker)

**Fontes ativas:**

| Fonte | Tipo | O que captura |
|---|---|---|
| `"System.Net.Http"` | Trace | Chamadas HTTP de saída do consumer |

**Sinais exportados:**
- **Traces**: spans do consumer (chamadas ao gateway de pagamento, etc.)

---

## 4. Configuração

```json
// appsettings.json
{
  "Otel": {
    "ServiceName": "billing-engine-api",
    "Endpoint":    "http://localhost:4317"
  }
}
```

Para o Worker (`appsettings.json` do Worker):
```json
{
  "Otel": {
    "ServiceName": "billing-engine-worker",
    "Endpoint":    "http://localhost:4317"
  }
}
```

### 4.1 Variáveis de ambiente

```bash
Otel__ServiceName=billing-engine-api
Otel__Endpoint=http://otel-collector:4317
```

---

## 5. Health Checks

### Endpoints

| Endpoint | Propósito | Dependências verificadas |
|---|---|---|
| `GET /health/live` | **Liveness**: processo está vivo | Nenhuma (retorna sempre `Healthy`) |
| `GET /health/ready` | **Readiness**: pronto para tráfego | PostgreSQL, Redis, RabbitMQ |

### Exemplo de resposta `/health/ready`

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgres", "status": "Healthy", "durationMs": 3 },
    { "name": "redis",    "status": "Healthy", "durationMs": 1 },
    { "name": "rabbitmq", "status": "Healthy", "durationMs": 5 }
  ]
}
```

Quando uma dependência está indisponível:
```json
{
  "status": "Unhealthy",
  "checks": [
    { "name": "postgres", "status": "Unhealthy", "description": "Connection refused", "durationMs": 5001 },
    { "name": "redis",    "status": "Healthy",   "durationMs": 1 },
    { "name": "rabbitmq", "status": "Healthy",   "durationMs": 5 }
  ]
}
```

### Kubernetes probes

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 15
  failureThreshold: 3
```

---

## 6. Backends suportados

### Jaeger (desenvolvimento)

```yaml
# docker-compose.yml
jaeger:
  image: jaegertracing/all-in-one:1.58
  ports:
    - "4317:4317"   # OTLP gRPC
    - "16686:16686" # Jaeger UI
```

### Grafana Tempo + Prometheus (produção)

```yaml
otel-collector:
  image: otel/opentelemetry-collector-contrib:0.100.0
  command: ["--config=/etc/otel/config.yaml"]
  volumes:
    - ./otel-config.yaml:/etc/otel/config.yaml
  ports:
    - "4317:4317"
```

```yaml
# otel-config.yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317

exporters:
  otlp/tempo:
    endpoint: tempo:4317
    tls:
      insecure: true
  prometheus:
    endpoint: "0.0.0.0:8889"

service:
  pipelines:
    traces:
      receivers: [otlp]
      exporters: [otlp/tempo]
    metrics:
      receivers: [otlp]
      exporters: [prometheus]
```

---

## 7. Limitações Conhecidas

- `OpenTelemetry.Exporter.OpenTelemetryProtocol` v1.10.0 tem advisory de gravidade moderada (GHSA-4625-4j76-fww9) — nenhuma versão `1.x` disponível é isenta; monitorar releases futuras e atualizar quando uma versão corrigida for lançada
- Instrumentação EF Core não incluída — queries SQL não aparecem nos traces
- Logs não são enviados via OTLP; apenas traces e métricas são exportados
- Worker não emite métricas de contagem de mensagens processadas — pendente instrumentação customizada

---

## 8. Histórico de Alterações

| Data       | Versão | Autor                | Descrição |
|------------|--------|----------------------|-----------|
| 2026-10-12 | 1.0    | Equipe BillingEngine | Criação — OTel SDK (traces + métricas), OTLP exporter, health checks com probes PostgreSQL/Redis/RabbitMQ |
