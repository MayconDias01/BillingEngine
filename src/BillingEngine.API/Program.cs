using BillingEngine.API.Endpoints;
using BillingEngine.API.Middleware;
using BillingEngine.Application.Behaviors;
using BillingEngine.Application.Commands.Auth.Register;
using BillingEngine.Infrastructure.DependencyInjection;
using BillingEngine.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Globalization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Infraestrutura ────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── CQRS / MediatR ────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssembly(typeof(RegisterCommand).Assembly);

// ── Autorização ───────────────────────────────────────────────────────────
builder.Services.AddAuthorization();

// ── Rate Limiting ─────────────────────────────────────────────────────────
// Sliding window: 5 requests/minute per IP on auth endpoints.
// Partitioned by remote IP so one client cannot exhaust the global limit.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = 5,
                Window               = TimeSpan.FromMinutes(1),
                SegmentsPerWindow    = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { type = "RateLimitExceeded", message = "Too many requests. Please try again later." }, ct);
    };
});

// ── Observabilidade (OpenTelemetry) ───────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: builder.Configuration["Otel:ServiceName"] ?? "billing-engine-api",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        // .NET 9 built-in activity sources — no separate instrumentation package needed.
        .AddSource("Microsoft.AspNetCore")
        .AddSource("System.Net.Http")
        .AddOtlpExporter(o => o.Endpoint =
            new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317")))
    .WithMetrics(metrics => metrics
        // .NET 9 built-in meters for HTTP server and client traffic.
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
        .AddMeter("System.Net.Http")
        .AddOtlpExporter(o => o.Endpoint =
            new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317")));

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "BillingEngine API",
        Version     = "v1",
        Description = "Motor de cobrança recorrente — documentação da API REST.",
        Contact     = new OpenApiContact
        {
            Name  = "Equipe BillingEngine",
            Email = "devops@billing.internal"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description  = "JWT Bearer. Formato: Bearer {token}",
        Name         = "Authorization",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Health Checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddInfrastructureHealthChecks(builder.Configuration);

// ── Pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

// Applies pending migrations on startup.
// For multi-instance production deployments, use a dedicated migration job instead.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BillingEngine API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
});

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapCustomerEndpoints();
app.MapPlanEndpoints();
app.MapSubscriptionEndpoints();
app.MapBillingEndpoints();

// Liveness: is the process alive? No external dependency checks.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: are all dependencies reachable? (PostgreSQL, Redis, RabbitMQ)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate      = check => check.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs  = (int)e.Value.Duration.TotalMilliseconds
            })
        });
    }
});

app.Run();
