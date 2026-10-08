using FluentValidation;

namespace BillingEngine.Application.Commands.Auth.VerifyMfa;

public class VerifyMfaValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}
