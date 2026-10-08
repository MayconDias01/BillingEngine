using BillingEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingEngine.Infrastructure.Persistence.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.OwnsOne(p => p.BillingCycle);
    }
}
