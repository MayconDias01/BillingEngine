using MediatR;

namespace BillingEngine.Application.Commands.Customers.CreateCustomer;

public record CreateCustomerCommand(
    Guid UserId,
    string Name,
    string Email,
    string Document) : IRequest<Guid>;
