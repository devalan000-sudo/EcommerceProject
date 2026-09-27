namespace Catalog.Api.Entities;

/// <summary>
/// Reglas derivadas de configuración de tienda, separadas de sus campos persistentes.
/// </summary>
public partial class StoreSettings
{
    public bool HasBranding() => !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(LogoUrl);

    public bool HasContactInformation() => !string.IsNullOrWhiteSpace(ContactEmail) || !string.IsNullOrWhiteSpace(ContactPhone);
}
