using BillingEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingEngine.Infrastructure.Persistence.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.GatewayCustomerId).IsRequired().HasMaxLength(256);
        builder.Property(s => s.GatewayPaymentMethodId).IsRequired().HasMaxLength(256);
        builder.HasIndex(s => new { s.CustomerId, s.PlanId, s.Status });
    }
}
