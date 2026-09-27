using System.Text.Json.Serialization;

namespace Catalog.Api.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<ShippingKind>))]
public enum ShippingKind { FlatRate, Free, Pickup }

public class ShippingOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ShippingKind Kind { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}
