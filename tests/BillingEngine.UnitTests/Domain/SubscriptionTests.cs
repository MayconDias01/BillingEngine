using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Enums;
using FluentAssertions;

namespace BillingEngine.UnitTests.Domain;

public class SubscriptionTests
{
    [Fact]
    public void Create_ShouldSetStatusActive()
    {
        var subscription = Subscription.Create(
            Guid.NewGuid(), Guid.NewGuid(), PaymentGateway.Fake, "cus_123", "pm_123");

        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void Cancel_ShouldSetStatusCancelled()
    {
        var subscription = Subscription.Create(
            Guid.NewGuid(), Guid.NewGuid(), PaymentGateway.Fake, "cus_123", "pm_123");

        subscription.Cancel();

        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
    }
}
