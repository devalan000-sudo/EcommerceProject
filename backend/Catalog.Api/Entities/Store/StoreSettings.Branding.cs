namespace Catalog.Api.Entities;

public partial class StoreSettings
{
    public string LogoUrl { get; set; } = "";
    public string HeroImageUrl { get; set; } = "";
    public string Announcement { get; set; } = "";
    public string HeroTitle { get; set; } = "";
    public string HeroDescription { get; set; } = "";
    public string FooterDescription { get; set; } = "";
    public string PrimaryColor { get; set; } = "#143f35";
    public string BackgroundColor { get; set; } = "#f7f7f2";
    public string AccentColor { get; set; } = "#d9e3b4";
    public string FaviconUrl { get; set; } = "";
}
