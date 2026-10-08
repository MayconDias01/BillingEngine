namespace BillingEngine.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string TotpSecret { get; private set; } = default!;
    public bool IsMfaEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash, string encryptedTotpSecret)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            TotpSecret = encryptedTotpSecret,
            IsMfaEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
