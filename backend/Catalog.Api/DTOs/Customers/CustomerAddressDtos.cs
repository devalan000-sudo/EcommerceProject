using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.DTOs;

public class DeliveryAddressDto : IValidatableObject
{
    [Required(ErrorMessage = "Ingresa calle."), StringLength(100)] public string Street { get; set; } = "";
    [Required(ErrorMessage = "Ingresa número exterior o S/N."), StringLength(20)] public string ExteriorNumber { get; set; } = "";
    [StringLength(20)] public string? InteriorNumber { get; set; }
    [Required(ErrorMessage = "Ingresa colonia."), StringLength(80)] public string Neighborhood { get; set; } = "";
    [Required(ErrorMessage = "Ingresa ciudad o municipio."), StringLength(80)] public string City { get; set; } = "";
    [Required(ErrorMessage = "Ingresa estado."), StringLength(80)] public string State { get; set; } = "";
    [Required, StringLength(12), RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9 -]{1,10}[A-Za-z0-9]$")]
    public string PostalCode { get; set; } = "";
    [Required(ErrorMessage = "Ingresa país."), StringLength(60)] public string Country { get; set; } = "México";
    [StringLength(150)] public string? References { get; set; }

    public string FormatAddress() => string.Join(", ", new[] {
        $"{Street?.Trim()} {ExteriorNumber?.Trim()}",
        string.IsNullOrWhiteSpace(InteriorNumber) ? null : $"Int. {InteriorNumber.Trim()}",
        Neighborhood?.Trim(), City?.Trim(), State?.Trim(), $"CP {PostalCode?.Trim()}", Country?.Trim(),
        string.IsNullOrWhiteSpace(References) ? null : $"Referencias: {References.Trim()}"
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if ((Country?.Trim().ToUpperInvariant() is "MÉXICO" or "MEXICO" or "MX") && !System.Text.RegularExpressions.Regex.IsMatch(PostalCode ?? "", @"^[0-9]{5}$"))
            yield return new("El código postal de México debe tener cinco dígitos.", [nameof(PostalCode)]);
        if (FormatAddress().Length > 500)
            yield return new("La dirección completa, incluyendo referencias, debe tener como máximo 500 caracteres.");
    }
}

public class CustomerAddressWriteDto : DeliveryAddressDto
{
    [Required, StringLength(40)] public string Label { get; set; } = "";
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [StringLength(25), RegularExpression(@"^\+?[0-9 ()-]{7,25}$")] public string? Phone { get; set; }
    public bool IsDefault { get; set; }
    public Guid? Version { get; set; }
    public override IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var error in base.Validate(context)) yield return error;
        if (!string.IsNullOrWhiteSpace(Phone) && Phone.Count(char.IsDigit) < 7)
            yield return new("El teléfono debe contener al menos 7 dígitos.", [nameof(Phone)]);
    }
}

public class CustomerAddressResponseDto : CustomerAddressWriteDto
{
    public int Id { get; set; }
    public string FormattedAddress => FormatAddress();
}

public record DeleteCustomerAddressDto([Required] Guid? Version);
