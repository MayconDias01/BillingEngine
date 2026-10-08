namespace BillingEngine.Application.Commands.Auth.Register;

public record RegisterResult(Guid UserId, string TotpSecret, string QrUri);
