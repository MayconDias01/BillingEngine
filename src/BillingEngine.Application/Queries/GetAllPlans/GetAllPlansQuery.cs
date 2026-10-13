using MediatR;

namespace BillingEngine.Application.Queries.GetAllPlans;

public record GetAllPlansQuery : IRequest<IEnumerable<PlanDto>>;
