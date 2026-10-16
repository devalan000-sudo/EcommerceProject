using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de StoreSettings: tabla única (Id=1), longitudes, valores por defecto y versión concurrente.
public class StoreSettingsConfiguration : IEntityTypeConfiguration<StoreSettings>
{
    public void Configure(EntityTypeBuilder<StoreSettings> b)
    {
        b.ToTable("StoreSettings", t => t.HasCheckConstraint("CK_StoreSettings_Singleton", "[Id] = 1"));
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Name).HasMaxLength(100);
        b.Property(s => s.WhatsAppPhone).HasMaxLength(15);
        b.Property(s => s.LogoUrl).HasMaxLength(2048);
        b.Property(s => s.HeroImageUrl).HasMaxLength(2048);
        b.Property(s => s.Announcement).HasMaxLength(160);
        b.Property(s => s.HeroTitle).HasMaxLength(100);
        b.Property(s => s.HeroDescription).HasMaxLength(500);
        b.Property(s => s.FooterDescription).HasMaxLength(500);
        b.Property(s => s.PrimaryColor).HasMaxLength(7).HasDefaultValue("#143f35");
        b.Property(s => s.BackgroundColor).HasMaxLength(7).HasDefaultValue("#f7f7f2");
        b.Property(s => s.AccentColor).HasMaxLength(7).HasDefaultValue("#d9e3b4");
        b.Property(s => s.FaviconUrl).HasMaxLength(2048).HasDefaultValue("");
        b.Property(s => s.InstagramUrl).HasMaxLength(2048).HasDefaultValue("");
        b.Property(s => s.FacebookUrl).HasMaxLength(2048).HasDefaultValue("");
        b.Property(s => s.TikTokUrl).HasMaxLength(2048).HasDefaultValue("");
        b.Property(s => s.ContactEmail).HasMaxLength(254).HasDefaultValue("");
        b.Property(s => s.ContactPhone).HasMaxLength(30).HasDefaultValue("");
        b.Property(s => s.BusinessAddress).HasMaxLength(500).HasDefaultValue("");
        b.Property(s => s.BusinessHours).HasMaxLength(500).HasDefaultValue("");
        b.Property(s => s.ShippingPolicy).HasMaxLength(6000).HasDefaultValue("");
        b.Property(s => s.ReturnsPolicy).HasMaxLength(6000).HasDefaultValue("");
        b.Property(s => s.Version).IsConcurrencyToken();
    }
}
