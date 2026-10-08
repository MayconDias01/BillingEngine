using BillingEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingEngine.Infrastructure.Persistence.Configurations;

public class BillingConfiguration : IEntityTypeConfiguration<Billing>
{
    public void Configure(EntityTypeBuilder<Billing> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Amount).HasPrecision(18, 2);
        builder.Property(b => b.Currency).IsRequired().HasMaxLength(3);
        builder.Property(b => b.IdempotencyKey).IsRequired().HasMaxLength(256);
        builder.HasIndex(b => b.IdempotencyKey).IsUnique();
    }
}
