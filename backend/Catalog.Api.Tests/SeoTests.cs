using System.Text.Json;
using System.Xml.Linq;
using Catalog.Api.Data;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

public class SeoTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly SeoService seo;
    private readonly int productId;
    public SeoTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        db.StoreSettings.Add(new() { Name = "Tienda & estilo", HeroDescription = "Nuestra colección", HeroImageUrl = "https://example.test/hero.jpg" });
        var p = new Product { CategoryId = 1, Name = "Playera </script><script>alert(1)</script>", Description = "Descripción < especial", Price = 199.50m, ImageUrl = "https://example.test/product.jpg" };
        db.Products.Add(p); db.SaveChanges(); productId = p.Id;
        db.ProductVariants.Add(new() { ProductId = p.Id, Size = "M", Color = "Rojo", Stock = 1 }); db.SaveChanges();
        seo = Service("https://tienda.example", true);
    }
    private SeoService Service(string origin, bool enabled) => new(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Seo:PublicUrl"] = origin,
        ["Seo:AllowIndexing"] = enabled.ToString()
    }).Build());
    [Fact]
    public async Task ProductHasOriginalPriceCanonicalAndSafeStructuredData()
    {
        var page = await seo.PageAsync($"/products/{productId}", default);
        using var json = JsonDocument.Parse(page.JsonLd!);
        Assert.Equal("199.50", json.RootElement.GetProperty("offers").GetProperty("price").GetString());
        Assert.Equal("https://schema.org/InStock", json.RootElement.GetProperty("offers").GetProperty("availability").GetString());
        Assert.DoesNotContain("</script>", page.JsonLd!);
        var html = seo.Render("<head><title>Old</title><meta name=\"robots\" content=\"noindex, nofollow\" /></head>", page);
        Assert.DoesNotContain("<script>alert", html); Assert.DoesNotContain("noindex", html);
        Assert.Contains($"https://tienda.example/products/{productId}", html); Assert.Contains("og:image", html);
    }
    [Fact]
    public async Task ReservationsAndInactiveVariantsDetermineAvailability()
    {
        var variant = db.ProductVariants.Single();
        var order = new Order { Id = Guid.NewGuid(), Number = "SEO", Customer = new Customer { NormalizedEmail = "SEO@EXAMPLE.TEST" }, StripeHold = true };
        db.Orders.Add(order); db.SaveChanges();
        db.InventoryReservations.Add(new() { OrderId = order.Id, VariantId = variant.Id, Quantity = 1, ExpiresAtUtc = DateTime.UtcNow.AddDays(-1) }); db.SaveChanges();
        Assert.Contains("OutOfStock", (await seo.PageAsync($"/products/{productId}", default)).JsonLd!);
        db.InventoryReservations.Single().Released = true; db.SaveChanges();
        Assert.Contains("InStock", (await seo.PageAsync($"/products/{productId}", default)).JsonLd!);
        variant.IsActive = false; db.SaveChanges();
        Assert.Contains("OutOfStock", (await seo.PageAsync($"/products/{productId}", default)).JsonLd!);
    }
    [Fact]
    public async Task HiddenAndMissingProductsReturn404AndNoStructuredData()
    {
        db.Products.Single().IsActive = false; db.SaveChanges();
        foreach (var path in new[] { $"/products/{productId}", "/products/99999", "/desconocido" })
        {
            var page = await seo.PageAsync(path, default); Assert.Equal(404, page.Status); Assert.False(page.Public); Assert.Null(page.JsonLd);
        }
        Assert.DoesNotContain($"/products/{productId}", await seo.SitemapAsync(1, default));
    }
    [Theory]
    [InlineData("/account")]
    [InlineData("/account/security")]
    [InlineData("/checkout")]
    [InlineData("/admin")]
    public async Task PrivatePagesAreNeverIndexed(string path)
    {
        var page = await seo.PageAsync(path, default);
        var html = seo.Render("<head><title>Old</title></head>", page);
        Assert.Contains("noindex", html); Assert.DoesNotContain("canonical", html); Assert.Null(page.JsonLd);
    }
    [Fact]
    public async Task SitemapsContainOnlyCanonicalPublicRoutes()
    {
        Assert.Contains("/sitemaps/1.xml", await seo.SitemapAsync(null, default));
        var pages = XDocument.Parse(await seo.SitemapAsync(0, default));
        Assert.Equal(3, pages.Root!.Elements().Count());
        Assert.Contains("https://tienda.example/products/", await seo.SitemapAsync(1, default));
        Assert.DoesNotContain("account", pages.ToString()); Assert.Contains("Sitemap:", seo.Robots());
    }
    [Fact]
    public async Task LocalAndDisabledEnvironmentsDoNotAdvertiseIndexing()
    {
        var local = Service("http://localhost:5080", true);
        Assert.False(local.Indexable); Assert.Contains("Disallow: /", local.Robots());
        Assert.Empty(XDocument.Parse(await local.SitemapAsync(null, default)).Root!.Elements());
        Assert.False(Service("https://tienda.example", false).Indexable);
        Assert.Throws<InvalidOperationException>(() => Service("https://user:pass@example.test", true).Origin);
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
