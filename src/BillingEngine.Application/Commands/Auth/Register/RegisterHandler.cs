using MediatR;

namespace BillingEngine.Application.Commands.Auth.Register;

public class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    public Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
