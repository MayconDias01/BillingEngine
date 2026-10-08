using BillingEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingEngine.Infrastructure.Persistence.Configurations;

public class BillingAttemptConfiguration : IEntityTypeConfiguration<BillingAttempt>
{
    public void Configure(EntityTypeBuilder<BillingAttempt> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.GatewayTransactionId).HasMaxLength(256);
        builder.Property(a => a.FailureCode).HasMaxLength(128);
        builder.Property(a => a.FailureReason).HasMaxLength(1024);
    }
}
