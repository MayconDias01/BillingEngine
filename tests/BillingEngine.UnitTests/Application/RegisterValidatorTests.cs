using BillingEngine.Application.Commands.Auth.Register;
using FluentAssertions;

namespace BillingEngine.UnitTests.Application;

public class RegisterValidatorTests
{
    private readonly RegisterValidator _validator = new();

    [Fact]
    public void Validate_WithValidData_ShouldPass()
    {
        var command = new RegisterCommand("user@example.com", "StrongPass1!");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyEmail_ShouldFail()
    {
        var command = new RegisterCommand("", "StrongPass1!");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithShortPassword_ShouldFail()
    {
        var command = new RegisterCommand("user@example.com", "short");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
