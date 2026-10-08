using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Interfaces;
using BillingEngine.Infrastructure.Caching;
using BillingEngine.Infrastructure.Gateways;
using BillingEngine.Infrastructure.Messaging;
using BillingEngine.Infrastructure.Persistence;
using BillingEngine.Infrastructure.Persistence.Repositories;
using BillingEngine.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BillingEngine.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Persistência ──────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        // ── Segurança ─────────────────────────────────────────────────────
        // JwtTokenService é instanciado diretamente para expor a chave pública de validação
        // antes de finalizar a construção do contêiner DI.
        var jwtService = new JwtTokenService(configuration);
        services.AddSingleton<ITokenService>(jwtService);

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        // ── Autenticação JWT Bearer ────────────────────────────────────────
        // Centralizado aqui para manter a chave de validação no mesmo local
        // onde é criada — sem precisar re-carregar o certificado em Program.cs.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer            = true,
                    ValidIssuer               = configuration["Auth:Issuer"] ?? "billing-engine",
                    ValidateAudience          = false,
                    ValidateLifetime          = true,
                    ValidateIssuerSigningKey  = true,
                    IssuerSigningKey          = jwtService.GetValidationKey(),
                    // Tolerância de 30 s para drift de relógio entre serviços
                    ClockSkew                 = TimeSpan.FromSeconds(30)
                };
            });

        // ── Mensageria e Cache ────────────────────────────────────────────
        // Implementações serão preenchidas no Passo 5
        services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
        services.AddSingleton<IIdempotencyService, RedisIdempotencyService>();
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

        return services;
    }
}
