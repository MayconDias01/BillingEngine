using BillingEngine.Domain.Exceptions;
using BillingEngine.Domain.ValueObjects;
using FluentAssertions;

namespace BillingEngine.UnitTests.Domain;

public class EmailTests
{
    [Fact]
    public void Create_WithValidEmail_ShouldSucceed()
    {
        var email = new Email("user@example.com");

        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Create_WithInvalidEmail_ShouldThrow()
    {
        var act = () => new Email("invalid-email");

        act.Should().Throw<DomainException>();
    }
}
