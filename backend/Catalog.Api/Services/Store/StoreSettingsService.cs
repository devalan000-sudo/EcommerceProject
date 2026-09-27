using System.ComponentModel.DataAnnotations;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class StoreSettingsService(CatalogDbContext db)
{
    public async Task<StoreSettingsResponseDto?> GetAsync(CancellationToken ct)
    {
        var settings = await db.StoreSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        return settings is null ? null : Response(settings);
    }

    public async Task<StoreSettingsResponseDto> SaveAsync(StoreSettingsWriteDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        var settings = await db.StoreSettings.SingleOrDefaultAsync(s => s.Id == 1, ct);
        if ((settings?.Version ?? Guid.Empty) != dto.Version) throw new DbUpdateConcurrencyException();
        if (settings is null) { settings = new StoreSettings(); db.StoreSettings.Add(settings); }
        settings.Name = dto.Name.Trim();
        settings.WhatsAppPhone = dto.WhatsAppPhone ?? "";
        settings.LogoUrl = dto.LogoUrl ?? "";
        settings.HeroImageUrl = dto.HeroImageUrl ?? "";
        settings.Announcement = dto.Announcement.Trim();
        settings.HeroTitle = dto.HeroTitle.Trim();
        settings.HeroDescription = dto.HeroDescription.Trim();
        settings.FooterDescription = dto.FooterDescription.Trim();
        settings.PrimaryColor = dto.PrimaryColor?.Trim() ?? "#143f35";
        settings.BackgroundColor = dto.BackgroundColor?.Trim() ?? "#f7f7f2";
        settings.AccentColor = dto.AccentColor?.Trim() ?? "#d9e3b4";
        settings.FaviconUrl = dto.FaviconUrl?.Trim() ?? "";
        settings.InstagramUrl = dto.InstagramUrl?.Trim() ?? "";
        settings.FacebookUrl = dto.FacebookUrl?.Trim() ?? "";
        settings.TikTokUrl = dto.TikTokUrl?.Trim() ?? "";
        settings.ContactEmail = dto.ContactEmail?.Trim() ?? "";
        settings.ContactPhone = dto.ContactPhone?.Trim() ?? "";
        settings.BusinessAddress = dto.BusinessAddress?.Trim() ?? "";
        settings.BusinessHours = dto.BusinessHours?.Trim() ?? "";
        settings.ShippingPolicy = dto.ShippingPolicy?.Trim() ?? "";
        settings.ReturnsPolicy = dto.ReturnsPolicy?.Trim() ?? "";
        settings.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return Response(settings);
    }

    private static StoreSettingsResponseDto Response(StoreSettings s) => new(s.Name, s.WhatsAppPhone,
        s.LogoUrl, s.HeroImageUrl, s.Announcement, s.HeroTitle, s.HeroDescription, s.FooterDescription, s.Version,
        s.PrimaryColor, s.BackgroundColor, s.AccentColor, s.FaviconUrl, s.InstagramUrl, s.FacebookUrl, s.TikTokUrl, s.ContactEmail, s.ContactPhone, s.BusinessAddress, s.BusinessHours, s.ShippingPolicy, s.ReturnsPolicy);
}
