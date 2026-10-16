using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.DTOs;

public class ManualPaymentDto : IValidatableObject
{
    [Required] public Guid? RequestId { get; set; }
    [Required] public Guid? Version { get; set; }
    [Required, RegularExpression("^(Cash|Transfer|Other)$")] public string Method { get; set; } = "";
    [StringLength(200)] public string? Reference { get; set; }
    [Required, StringLength(100)] public string ShippingName { get; set; } = "";
    [Required, Range(typeof(decimal), "0", "999999.99")] public decimal? ShippingCost { get; set; }
    [Required, Range(typeof(decimal), "0.01", "9999999999999999.99")] public decimal? AmountReceived { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (RequestId == Guid.Empty) yield return new("El identificador de confirmación no es válido.");
        if (ShippingCost.HasValue && decimal.Round(ShippingCost.Value, 2) != ShippingCost.Value)
            yield return new("El costo de entrega debe tener como máximo dos decimales.");
        if (AmountReceived.HasValue && decimal.Round(AmountReceived.Value, 2) != AmountReceived.Value)
            yield return new("El importe recibido debe tener como máximo dos decimales.");
    }
}
