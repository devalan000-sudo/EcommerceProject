namespace Catalog.Api.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int? ProductId { get; set; }
    public int? VariantId { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? Details { get; set; }
    public decimal LineTotal { get; set; }
}
