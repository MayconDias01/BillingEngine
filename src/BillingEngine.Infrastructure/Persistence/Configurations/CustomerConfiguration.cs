using BillingEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingEngine.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(256);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.EncryptedDocument).IsRequired();
        builder.HasIndex(c => c.Email);
        builder.HasIndex(c => c.UserId);
    }
}
