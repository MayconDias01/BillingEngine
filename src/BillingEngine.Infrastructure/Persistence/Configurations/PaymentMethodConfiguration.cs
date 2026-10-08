using BillingEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingEngine.Infrastructure.Persistence.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.HasKey(pm => pm.Id);
        builder.Property(pm => pm.GatewayCustomerId).IsRequired().HasMaxLength(256);
        builder.Property(pm => pm.GatewayPaymentMethodId).IsRequired().HasMaxLength(256);
    }
}
