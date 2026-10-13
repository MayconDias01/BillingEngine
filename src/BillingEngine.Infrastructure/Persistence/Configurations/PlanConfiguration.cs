using BillingEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingEngine.Infrastructure.Persistence.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    private static readonly Guid BasicPlanId =
        new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProPlanId =
        new("10000000-0000-0000-0000-000000000002");
    private static readonly Guid EnterprisePlanId =
        new("10000000-0000-0000-0000-000000000003");

    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);

        builder.OwnsOne(p => p.BillingCycle, owned =>
        {
            owned.HasData(
                new { PlanId = BasicPlanId,     IntervalDays = 30  },
                new { PlanId = ProPlanId,       IntervalDays = 30  },
                new { PlanId = EnterprisePlanId, IntervalDays = 365 }
            );
        });

        builder.HasData(
            new { Id = BasicPlanId,      Name = "Basic",      Price = 9.99m,   Currency = "USD", IsActive = true },
            new { Id = ProPlanId,        Name = "Pro",        Price = 29.99m,  Currency = "USD", IsActive = true },
            new { Id = EnterprisePlanId, Name = "Enterprise", Price = 299.99m, Currency = "USD", IsActive = true }
        );
    }
}
