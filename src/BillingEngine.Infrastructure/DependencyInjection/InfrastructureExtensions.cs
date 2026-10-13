using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Interfaces;
using BillingEngine.Infrastructure.Caching;
using BillingEngine.Infrastructure.Gateways;
using BillingEngine.Infrastructure.Messaging;
using BillingEngine.Infrastructure.Persistence;
using BillingEngine.Infrastructure.Persistence.Repositories;
using BillingEngine.Infrastructure.Security;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using RabbitMQ.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace BillingEngine.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
    /// <param name="configureMassTransit">
    /// Optional callback to register MassTransit consumers.
    /// Called by the Worker to add <c>BillingRequestedConsumer</c>.
    /// The API passes null (publish-only, no consumers).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureMassTransit = null)
    {
        // ── Persistência ──────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();

        // ── Segurança ─────────────────────────────────────────────────────
        // JwtTokenService é instanciado diretamente para expor a chave pública de validação
        // antes de finalizar a construção do contêiner DI.
        var jwtService = new JwtTokenService(configuration);
        services.AddSingleton<ITokenService>(jwtService);

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        // ── Autenticação JWT Bearer ────────────────────────────────────────
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidIssuer              = configuration["Auth:Issuer"] ?? "billing-engine",
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = jwtService.GetValidationKey(),
                    ClockSkew                = TimeSpan.FromSeconds(30)
                };
            });

        // ── Mensageria (MassTransit + RabbitMQ) ───────────────────────────
        services.AddMassTransit(x =>
        {
            // Consumers registered here when called from the Worker.
            configureMassTransit?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration["RabbitMQ:Host"] ?? "localhost", h =>
                {
                    h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                    h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                });
                cfg.ConfigureEndpoints(context);
            });
        });

        // Scoped because IPublishEndpoint (from MassTransit) is scoped.
        services.AddScoped<IMessageBus, RabbitMqMessageBus>();

        // ── Cache (Redis) ─────────────────────────────────────────────────
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration["Redis:ConnectionString"] ?? "localhost:6379"));

        services.AddSingleton<IIdempotencyService, RedisIdempotencyService>();

        // ── Gateways ──────────────────────────────────────────────────────
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

        return services;
    }

    /// <summary>
    /// Registers health checks for PostgreSQL, Redis and RabbitMQ.
    /// All checks carry the "ready" tag so they are only evaluated on /health/ready.
    /// </summary>
    public static IHealthChecksBuilder AddInfrastructureHealthChecks(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
    {
        var pgConn     = configuration.GetConnectionString("Postgres")!;
        var redisConn  = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        var rabbitHost = configuration["RabbitMQ:Host"]     ?? "localhost";
        var rabbitUser = configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = configuration["RabbitMQ:Password"] ?? "guest";

        return builder
            .AddNpgSql(pgConn,   name: "postgres", tags: ["ready"])
            .AddRedis(redisConn, name: "redis",    tags: ["ready"])
            .AddRabbitMQ(_ =>
            {
                var factory = new ConnectionFactory
                {
                    HostName = rabbitHost,
                    UserName = rabbitUser,
                    Password = rabbitPass
                };
                // RabbitMQ.Client v7 removed the sync CreateConnection; block on async.
                // Health checks run infrequently so this is acceptable.
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            }, name: "rabbitmq", tags: ["ready"]);
    }
}
