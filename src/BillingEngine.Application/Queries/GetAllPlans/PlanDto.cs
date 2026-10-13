namespace BillingEngine.Application.Queries.GetAllPlans;

public record PlanDto(
    Guid Id,
    string Name,
    decimal Price,
    string Currency,
    int IntervalDays);
