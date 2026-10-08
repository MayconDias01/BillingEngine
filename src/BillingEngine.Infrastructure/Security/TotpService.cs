using BillingEngine.Application.Interfaces;

namespace BillingEngine.Infrastructure.Security;

public class TotpService : ITotpService
{
    public string GenerateSecret()
        => throw new NotImplementedException();

    public string GetQrUri(string secret, string email)
        => throw new NotImplementedException();

    public bool Verify(string secret, string code)
        => throw new NotImplementedException();
}
