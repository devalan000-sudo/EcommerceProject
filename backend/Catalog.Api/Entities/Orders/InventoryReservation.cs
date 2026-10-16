namespace Catalog.Api.Entities;

public class InventoryReservation
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool Released { get; set; }
}
