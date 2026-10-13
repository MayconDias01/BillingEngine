using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Queries.GetAllPlans;

public class GetAllPlansHandler : IRequestHandler<GetAllPlansQuery, IEnumerable<PlanDto>>
{
    private readonly IPlanRepository _plans;

    public GetAllPlansHandler(IPlanRepository plans) => _plans = plans;

    public async Task<IEnumerable<PlanDto>> Handle(GetAllPlansQuery request, CancellationToken ct)
    {
        var plans = await _plans.GetAllActiveAsync(ct);
        return plans.Select(p => new PlanDto(
            p.Id,
            p.Name,
            p.Price,
            p.Currency,
            p.BillingCycle.IntervalDays));
    }
}
