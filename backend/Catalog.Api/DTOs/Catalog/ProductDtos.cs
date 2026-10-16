using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.DTOs;

public record ProductResponseDto(int Id, string Name, string Description, decimal Price,
    string ImageUrl, bool IsActive, int CategoryId, string CategoryName, long ViewCount,
    List<VariantResponseDto>? Variants = null, bool HasVariants = false);

public class ProductCreateDto : IValidatableObject
{
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(2000)] public string Description { get; set; } = "";
    [Range(typeof(decimal), "0.01", "9999999999.99")] public decimal Price { get; set; }
    [Required, StringLength(2048)] public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (decimal.Round(Price, 2) != Price)
            yield return new ValidationResult("Usa como máximo dos decimales.", [nameof(Price)]);
        if (!Uri.TryCreate(ImageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
            yield return new ValidationResult("La imagen debe tener una URL HTTP o HTTPS.", [nameof(ImageUrl)]);
    }
}

public class ProductUpdateDto : ProductCreateDto;
