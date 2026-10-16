namespace Catalog.Api.Entities;

// Una configuración por instalación; no mezcla negocios ni credenciales.
public partial class StoreSettings
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = "";
    public string WhatsAppPhone { get; set; } = "";
    public Guid Version { get; set; }
}
