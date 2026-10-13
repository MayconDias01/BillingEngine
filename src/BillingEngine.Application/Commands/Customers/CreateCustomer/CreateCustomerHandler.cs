using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Application.Commands.Customers.CreateCustomer;

public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, Guid>
{
    private readonly ICustomerRepository _customers;
    private readonly IEncryptionService  _encryption;

    public CreateCustomerHandler(ICustomerRepository customers, IEncryptionService encryption)
    {
        _customers  = customers;
        _encryption = encryption;
    }

    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        var encryptedDocument = _encryption.Encrypt(request.Document);
        var customer = Customer.Create(request.UserId, request.Name, request.Email, encryptedDocument);
        await _customers.AddAsync(customer, ct);
        return customer.Id;
    }
}
