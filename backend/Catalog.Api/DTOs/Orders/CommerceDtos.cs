using System.ComponentModel.DataAnnotations;
using Catalog.Api.Entities;

namespace Catalog.Api.DTOs;

public record VariantResponseDto(int Id, string Size, string Color, int Stock, int AvailableStock, bool IsActive, string? ImageUrl = null);
public class VariantWriteDto : IValidatableObject
{
    [Required, StringLength(30)] public string Size { get; set; } = "";
    [Required, StringLength(50)] public string Color { get; set; } = "";
    [Range(0, 1000000)] public int Stock { get; set; }
    [StringLength(2048)] public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!string.IsNullOrWhiteSpace(ImageUrl) && (!Uri.TryCreate(ImageUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo)))
            yield return new("La imagen debe ser una URL HTTP o HTTPS sin credenciales.", [nameof(ImageUrl)]);
    }
}
public class ShippingWriteDto : IValidatableObject
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(500)] public string Description { get; set; } = "";
    [EnumDataType(typeof(ShippingKind))] public ShippingKind Kind { get; set; }
    [Range(typeof(decimal), "0", "999999.99")] public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (decimal.Round(Price, 2) != Price || (Kind != ShippingKind.FlatRate && Price != 0))
            yield return new ValidationResult("Usa dos decimales como máximo y precio cero para envío gratuito o recogida.", [nameof(Price)]);
    }
}
public record ShippingResponseDto(int Id, string Name, string Description, ShippingKind Kind, decimal Price, bool IsActive);
