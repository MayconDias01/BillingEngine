namespace BillingEngine.Domain.Entities;

public class Customer
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string EncryptedDocument { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }

    private Customer() { }

    public static Customer Create(Guid userId, string name, string email, string encryptedDocument)
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Email = email,
            EncryptedDocument = encryptedDocument,
            CreatedAt = DateTime.UtcNow
        };
    }
}
