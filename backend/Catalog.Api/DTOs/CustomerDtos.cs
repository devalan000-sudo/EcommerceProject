using System.ComponentModel.DataAnnotations;
namespace Catalog.Api.DTOs;

public class CustomerProfileDto : IValidatableObject
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [StringLength(25), RegularExpression(@"^\+?[0-9 ()-]{7,25}$", ErrorMessage = "Introduce un teléfono válido.")]
    public string? Phone { get; set; }
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(500, MinimumLength = 10)] public string Address { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if ((Address?.Trim().Length ?? 0) < 10)
            yield return new ValidationResult("Introduce una dirección completa de al menos 10 caracteres.", [nameof(Address)]);
        if (!string.IsNullOrWhiteSpace(Phone) && Phone.Count(char.IsDigit) < 7)
            yield return new ValidationResult("El teléfono debe contener al menos 7 dígitos.", [nameof(Phone)]);
    }
}
public class CustomerRegisterDto : CustomerProfileDto
{
    [Required, StringLength(256, MinimumLength = 12)] public string Password { get; set; } = "";
}
public record CustomerLoginDto([Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(256)] string Password);
public record CustomerResponseDto(int Id, string FirstName, string LastName, string? Phone, string Email, string Address);

