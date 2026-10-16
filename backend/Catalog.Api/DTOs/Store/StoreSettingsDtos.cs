using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.DTOs;

public class StoreSettingsWriteDto : IValidatableObject
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(15), RegularExpression(@"^[1-9][0-9]{7,14}$", ErrorMessage = "Usa el código de país y el número, solo dígitos (8 a 15).")]
    public string WhatsAppPhone { get; set; } = "";
    [StringLength(2048)] public string LogoUrl { get; set; } = "";
    [StringLength(2048)] public string HeroImageUrl { get; set; } = "";
    [Required, StringLength(160)] public string Announcement { get; set; } = "";
    [Required, StringLength(100)] public string HeroTitle { get; set; } = "";
    [Required, StringLength(500)] public string HeroDescription { get; set; } = "";
    [Required, StringLength(500)] public string FooterDescription { get; set; } = "";
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$"), StringLength(7)] public string PrimaryColor { get; set; } = "#143f35";
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$"), StringLength(7)] public string BackgroundColor { get; set; } = "#f7f7f2";
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$"), StringLength(7)] public string AccentColor { get; set; } = "#d9e3b4";
    [StringLength(2048)] public string FaviconUrl { get; set; } = "";
    [StringLength(2048)] public string InstagramUrl { get; set; } = "";
    [StringLength(2048)] public string FacebookUrl { get; set; } = "";
    [StringLength(2048)] public string TikTokUrl { get; set; } = "";
    [StringLength(254)] public string ContactEmail { get; set; } = "";
    [StringLength(30)] public string ContactPhone { get; set; } = "";
    [StringLength(500)] public string BusinessAddress { get; set; } = "";
    [StringLength(500)] public string BusinessHours { get; set; } = "";
    [StringLength(6000)] public string ShippingPolicy { get; set; } = "";
    [StringLength(6000)] public string ReturnsPolicy { get; set; } = "";
    [Required] public Guid? Version { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!string.IsNullOrEmpty(ContactEmail) && !new EmailAddressAttribute().IsValid(ContactEmail))
            yield return new ValidationResult("Escribe un correo electrónico válido.", [nameof(ContactEmail)]);
        // Las superficies existentes usan texto blanco sobre principal y oscuro sobre fondo/acento.
        foreach (var (name, value, dark) in new[] { (nameof(PrimaryColor), PrimaryColor, true), (nameof(BackgroundColor), BackgroundColor, false), (nameof(AccentColor), AccentColor, false) })
        {
            if (value is null || !System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$")) continue;
            var luminance = Luminance(value);
            if (dark ? 1.05 / (luminance + .05) < 4.5 : (luminance + .05) / (Luminance("#182b25") + .05) < 7)
                yield return new ValidationResult(dark ? "El color principal debe ser más oscuro para leer el texto blanco." : "El fondo y el acento deben ser más claros para leer el texto oscuro.", [name]);
        }
        foreach (var (name, value) in new[] { (nameof(LogoUrl), LogoUrl), (nameof(HeroImageUrl), HeroImageUrl), (nameof(FaviconUrl), FaviconUrl), (nameof(InstagramUrl), InstagramUrl), (nameof(FacebookUrl), FacebookUrl), (nameof(TikTokUrl), TikTokUrl) })
        {
            if (string.IsNullOrEmpty(value)) continue;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var url) ||
                !(url.Scheme == "https" || (url.Scheme == "http" && url.IsLoopback)) || !string.IsNullOrEmpty(url.UserInfo))
                yield return new ValidationResult("Usa una URL HTTPS sin credenciales. HTTP solo se admite en localhost.", [name]);
        }
    }
    private static double Luminance(string hex)
    {
        double Channel(int start) { var c = Convert.ToInt32(hex.Substring(start, 2), 16) / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        return .2126 * Channel(1) + .7152 * Channel(3) + .0722 * Channel(5);
    }
}

public record StoreSettingsResponseDto(string Name, string WhatsAppPhone, string LogoUrl,
    string HeroImageUrl, string Announcement, string HeroTitle, string HeroDescription,
    string FooterDescription, Guid Version,
    string PrimaryColor,
    string BackgroundColor,
    string AccentColor,
    string FaviconUrl,
    string InstagramUrl,
    string FacebookUrl,
    string TikTokUrl,
    string ContactEmail,
    string ContactPhone,
    string BusinessAddress,
    string BusinessHours,
    string ShippingPolicy,
    string ReturnsPolicy);
