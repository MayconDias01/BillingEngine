using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Interfaces;
using BillingEngine.Infrastructure.Caching;
using BillingEngine.Infrastructure.Gateways;
using BillingEngine.Infrastructure.Messaging;
using BillingEngine.Infrastructure.Persistence;
using BillingEngine.Infrastructure.Persistence.Repositories;
using BillingEngine.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BillingEngine.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
        services.AddSingleton<IIdempotencyService, RedisIdempotencyService>();
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

        return services;
    }
}
