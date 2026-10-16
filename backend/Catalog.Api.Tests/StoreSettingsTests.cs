using System.ComponentModel.DataAnnotations;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class StoreSettingsTests
{
    private static StoreSettingsWriteDto Request() => new()
    {
        Name = "Mi tienda",
        Announcement = "Bienvenidos",
        HeroTitle = "Colección",
        HeroDescription = "Descubre",
        FooterDescription = "Gracias",
        Version = Guid.Empty
    };

    [Fact]
    public void AcceptsDefaultsWithoutOptionalContactAndRejectsInvalidEmail()
    {
        var dto = Request();
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        dto.ContactEmail = "incorrecto";
        Assert.Throws<ValidationException>(() => Validator.ValidateObject(dto, new ValidationContext(dto), true));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.test/image.png")]
    [InlineData("http://example.test/image.png")]
    public void RejectsUnsafePublicUrls(string url)
    {
        var dto = Request(); dto.FaviconUrl = url;
        Assert.Throws<ValidationException>(() => Validator.ValidateObject(dto, new ValidationContext(dto), true));
        dto.FaviconUrl = ""; dto.InstagramUrl = url;
        Assert.Throws<ValidationException>(() => Validator.ValidateObject(dto, new ValidationContext(dto), true));
    }

    [Theory]
    [InlineData("#ffffff", "#f7f7f2", "#d9e3b4")]
    [InlineData("#143f35", "#000000", "#d9e3b4")]
    [InlineData("#143f35", "#f7f7f2", "red")]
    public void RejectsUnreadableOrMalformedTheme(string primary, string background, string accent)
    {
        var dto = Request(); dto.PrimaryColor = primary; dto.BackgroundColor = background; dto.AccentColor = accent;
        Assert.Throws<ValidationException>(() => Validator.ValidateObject(dto, new ValidationContext(dto), true));
    }

    [Fact]
    public async Task PersistsPublicSettingsAndRejectsStaleVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options;
        await using var db = new CatalogDbContext(options); await db.Database.EnsureCreatedAsync();
        var service = new StoreSettingsService(db); var dto = Request();
        dto.PrimaryColor = "#123456"; dto.FaviconUrl = "https://example.test/icon.png";
        dto.InstagramUrl = "https://www.instagram.com/store"; dto.ContactEmail = "hola@example.test";
        dto.BusinessHours = "Lunes a viernes\n9–18 h"; dto.ShippingPolicy = "  Envíos nacionales  ";
        dto.ReturnsPolicy = "Consulta antes de devolver.";
        var saved = await service.SaveAsync(dto, default);
        var read = await service.GetAsync(default);
        Assert.Equal(saved, read); Assert.Equal("Envíos nacionales", saved.ShippingPolicy);
        Assert.Equal(dto.PrimaryColor, saved.PrimaryColor); Assert.Equal(dto.BusinessHours, saved.BusinessHours);
        Assert.Equal(dto.FaviconUrl, saved.FaviconUrl); Assert.Equal(dto.ContactEmail, saved.ContactEmail);
        Assert.Equal(dto.ReturnsPolicy, saved.ReturnsPolicy);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveAsync(dto, default));
        dto.Version = saved.Version; dto.FaviconUrl = ""; dto.ShippingPolicy = "";
        var cleared = await service.SaveAsync(dto, default);
        Assert.Equal("", cleared.FaviconUrl); Assert.Equal("", cleared.ShippingPolicy);
        Assert.NotEqual(saved.Version, cleared.Version);
    }
}
