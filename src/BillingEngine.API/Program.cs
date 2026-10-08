using BillingEngine.API.Endpoints;
using BillingEngine.API.Middleware;
using BillingEngine.Application.Behaviors;
using BillingEngine.Application.Commands.Auth.Register;
using BillingEngine.Infrastructure.DependencyInjection;
using FluentValidation;
using MediatR;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Infraestrutura: persistência, segurança, JWT Bearer, mensageria ──────
builder.Services.AddInfrastructure(builder.Configuration);

// ── CQRS / MediatR ────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly));

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssembly(typeof(RegisterCommand).Assembly);

// ── Autorização ───────────────────────────────────────────────────────────
builder.Services.AddAuthorization();

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "BillingEngine API",
        Version     = "v1",
        Description = "Motor de cobrança recorrente — documentação da API REST."
    });

    // Habilita o botão "Authorize" no Swagger UI para testar endpoints protegidos
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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHealthChecks();

// ── Pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BillingEngine API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapSubscriptionEndpoints();
app.MapBillingEndpoints();
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

app.Run();
