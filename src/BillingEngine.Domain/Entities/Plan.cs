using BillingEngine.Domain.ValueObjects;

namespace BillingEngine.Domain.Entities;

public class Plan
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = default!;
    public BillingCycle BillingCycle { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private Plan() { }

    public static Plan Create(string name, decimal price, string currency, BillingCycle cycle)
    {
        return new Plan
        {
            Id = Guid.NewGuid(),
            Name = name,
            Price = price,
            Currency = currency,
            BillingCycle = cycle,
            IsActive = true
        };
    }
}
