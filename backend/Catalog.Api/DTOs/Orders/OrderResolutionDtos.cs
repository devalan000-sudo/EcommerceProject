using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.DTOs;

public class CancelOrderDto
{
    [Required] public Guid? Version { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
public class RefundOrderDto : CancelOrderDto, IValidatableObject
{
    [Required] public Guid? RequestId { get; set; }
    public bool Restock { get; set; }
    public bool ManualRefundConfirmed { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (RequestId == Guid.Empty) yield return new("Identificador de reembolso inválido.");
    }
}
