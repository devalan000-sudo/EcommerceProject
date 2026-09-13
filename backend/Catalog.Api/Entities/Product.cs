namespace Catalog.Api.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public long ViewCount { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
