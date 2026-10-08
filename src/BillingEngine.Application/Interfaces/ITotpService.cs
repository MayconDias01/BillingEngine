namespace BillingEngine.Application.Interfaces;

public interface ITotpService
{
    string GenerateSecret();
    string GetQrUri(string secret, string email);
    bool Verify(string secret, string code);
}
