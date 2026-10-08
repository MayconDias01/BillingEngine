using MediatR;

namespace BillingEngine.Application.Commands.Auth.Login;

public class LoginHandler : IRequestHandler<LoginCommand, string>
{
    public Task<string> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
