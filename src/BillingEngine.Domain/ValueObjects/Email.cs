using System.Text.RegularExpressions;
using BillingEngine.Domain.Exceptions;

namespace BillingEngine.Domain.ValueObjects;

public partial record Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !EmailRegex().IsMatch(value))
            throw new DomainException($"Invalid email format: {value}");

        Value = value;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
