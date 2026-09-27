namespace Catalog.Api.Entities;

public class EmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventKey { get; set; } = "";
    public int CustomerId { get; set; }
    public Guid? OrderId { get; set; }
    public string Kind { get; set; } = "";
    public string? ProtectedToken { get; set; }
    public string? TokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(1);
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntilUtc { get; set; }
    public int Attempts { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ProviderMessageId { get; set; }
}
public class CustomerEmailToken
{
    public string Hash { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Purpose { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public Guid SecurityStamp { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}
