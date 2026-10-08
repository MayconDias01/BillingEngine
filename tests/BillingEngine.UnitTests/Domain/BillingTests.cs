using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Enums;
using FluentAssertions;

namespace BillingEngine.UnitTests.Domain;

public class BillingTests
{
    [Fact]
    public void Create_ShouldSetStatusPending()
    {
        var billing = Billing.Create(
            Guid.NewGuid(), 99.90m, "BRL", DateTime.UtcNow, "idem-123", PaymentGateway.Fake);

        billing.Status.Should().Be(BillingStatus.Pending);
    }

    [Fact]
    public void MarkAsPaid_ShouldSetPaidAt()
    {
        var billing = Billing.Create(
            Guid.NewGuid(), 99.90m, "BRL", DateTime.UtcNow, "idem-123", PaymentGateway.Fake);

        billing.MarkAsPaid();

        billing.PaidAt.Should().NotBeNull();
        billing.Status.Should().Be(BillingStatus.Paid);
    }
}
