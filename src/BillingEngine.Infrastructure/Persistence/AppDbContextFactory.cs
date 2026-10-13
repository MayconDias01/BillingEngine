using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BillingEngine.Infrastructure.Persistence;

/// <summary>
/// Factory de design-time para o EF Core CLI (<c>dotnet ef migrations add</c>).
/// Fornece um AppDbContext sem precisar inicializar toda a aplicação.
/// NÃO é usado em runtime — apenas pelo tooling EF durante o desenvolvimento.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Connection string de design-time — apenas para geração de migrations.
        // Em runtime, a string vem de IConfiguration via AddInfrastructure().
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=billing;Username=billing;Password=billing");

        return new AppDbContext(optionsBuilder.Options);
    }
}
