using System.ComponentModel.DataAnnotations;
using Catalog.Api.Entities;

namespace Catalog.Api.DTOs;

public class AdminOrderQuery : IValidatableObject
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [StringLength(100)] public string? Search { get; set; }
    [EnumDataType(typeof(PaymentMethod))] public PaymentMethod? PaymentMethod { get; set; }
    [EnumDataType(typeof(PaymentStatus))] public PaymentStatus? PaymentStatus { get; set; }
    [EnumDataType(typeof(FulfillmentStatus))] public FulfillmentStatus? FulfillmentStatus { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (From > To) yield return new("La fecha inicial debe ser anterior a la final.");
        if (To == DateOnly.MaxValue) yield return new("La fecha final está fuera del rango permitido.");
    }
}

public class FulfillmentUpdateDto
{
    [Required, EnumDataType(typeof(FulfillmentStatus))] public FulfillmentStatus? Status { get; set; }
    [Required] public Guid? Version { get; set; }
    [StringLength(100)] public string? Carrier { get; set; }
    [StringLength(100)] public string? TrackingNumber { get; set; }
}

public record AdminOrderRow(Guid Id, string Number, DateTime CreatedAtUtc, string FirstName, string LastName,
    string Email, PaymentMethod PaymentMethod, PaymentStatus PaymentStatus, FulfillmentStatus FulfillmentStatus,
    decimal? Total, string Currency);
public record AdminOrderPage(List<AdminOrderRow> Items, int Page, int PageSize, int TotalCount);
