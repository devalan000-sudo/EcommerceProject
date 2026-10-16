namespace Catalog.Api.Entities;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
    public string? ImageUrl { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public List<InventoryReservation> Reservations { get; set; } = [];
}
