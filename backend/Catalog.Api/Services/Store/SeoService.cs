using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public record SeoPage(string Title, string Description, string Path, string? Image, bool Public, int Status, string? JsonLd = null);

public class SeoService(CatalogDbContext db, IConfiguration config)
{
    // No usar Host del visitante para construir URLs canónicas.
    public string Origin
    {
        get
        {
            var value = config["Seo:PublicUrl"] ?? "http://localhost:5080";
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !(uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback)) ||
                uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
                throw new InvalidOperationException("Seo:PublicUrl debe ser el origen HTTPS de la tienda, sin rutas ni credenciales (HTTP solo localhost).");
            return uri.GetLeftPart(UriPartial.Authority);
        }
    }
    public bool Indexable => config.GetValue<bool>("Seo:AllowIndexing") && new Uri(Origin).Scheme == "https" && !new Uri(Origin).IsLoopback;
    private static string Short(string value)
    {
        var text = Regex.Replace(value, @"\s+", " ").Trim();
        return text.Length > 180 ? text[..177] + "…" : text;
    }
    private static string? ImageUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 ? uri.AbsoluteUri : null;

    public async Task<SeoPage> PageAsync(string path, CancellationToken ct)
    {
        path = "/" + path.Trim('/');
        if (path == "/admin" || path == "/checkout" || path == "/account" || path.StartsWith("/account/"))
            return new("Área privada", "Accede a tu cuenta de la tienda.", path, null, false, 200);
        var settings = await db.StoreSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        var name = settings?.Name ?? "Ecommerce-Boutique";
        var description = Short(settings?.HeroDescription ?? "Descubre nuestro catálogo y encuentra tus próximos favoritos.");
        var image = ImageUrl(settings?.HeroImageUrl) ?? ImageUrl(settings?.LogoUrl);
        if (path == "/") return new(name, description, path, image, true, 200);
        if (path == "/comprar") return new($"Catálogo | {name}", description, path, image, true, 200);
        if (path == "/informacion") return new($"Contacto y políticas | {name}", $"Consulta los datos de contacto, horarios y políticas de {name}.", path, image, true, 200);
        var match = Regex.Match(path, @"^/products/([0-9]+)$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
        {
            var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.IsActive, ct);
            if (product != null)
            {
                path = $"/products/{id}";
                var available = (await InventoryService.Query(db, id, false, DateTime.UtcNow).ToListAsync(ct)).Any(v => v.AvailableStock > 0);
                var productImage = ImageUrl(product.ImageUrl);
                var data = new Dictionary<string, object>
                {
                    ["@context"] = "https://schema.org",
                    ["@type"] = "Product",
                    ["name"] = product.Name,
                    ["description"] = product.Description,
                    ["url"] = Origin + path,
                    ["sku"] = product.Id.ToString(CultureInfo.InvariantCulture)
                };
                if (productImage != null) data["image"] = new[] { productImage };
                if (product.Price > 0) data["offers"] = new Dictionary<string, object>
                {
                    ["@type"] = "Offer",
                    ["url"] = Origin + path,
                    ["priceCurrency"] = "MXN",
                    ["price"] = product.Price.ToString("0.00", CultureInfo.InvariantCulture),
                    ["availability"] = "https://schema.org/" + (available ? "InStock" : "OutOfStock")
                };
                return new($"{product.Name} | {name}", Short(product.Description), path, productImage, true, 200, JsonSerializer.Serialize(data));
            }
        }
        return new($"Página no encontrada | {name}", "Esta página no está disponible.", path, null, false, 404);
    }

    public string Render(string template, SeoPage page)
    {
        static string E(string text) => WebUtility.HtmlEncode(text);
        var url = Origin + page.Path;
        var robots = Indexable && page.Public ? "index, follow" : "noindex, nofollow";
        var head = $"<meta name=\"seo-rendered\" content=\"true\" /><meta name=\"description\" content=\"{E(page.Description)}\" /><meta name=\"robots\" content=\"{robots}\" />";
        if (page.Public) head += $"<link rel=\"canonical\" href=\"{E(url)}\" /><meta property=\"og:url\" content=\"{E(url)}\" />";
        head += $"<meta property=\"og:type\" content=\"website\" /><meta property=\"og:locale\" content=\"es_MX\" /><meta property=\"og:title\" content=\"{E(page.Title)}\" /><meta property=\"og:description\" content=\"{E(page.Description)}\" />";
        head += $"<meta name=\"twitter:card\" content=\"{(page.Image is null ? "summary" : "summary_large_image")}\" /><meta name=\"twitter:title\" content=\"{E(page.Title)}\" /><meta name=\"twitter:description\" content=\"{E(page.Description)}\" />";
        if (page.Image != null) head += $"<meta property=\"og:image\" content=\"{E(page.Image)}\" /><meta property=\"og:image:alt\" content=\"{E(page.Title)}\" /><meta name=\"twitter:image\" content=\"{E(page.Image)}\" />";
        if (page.JsonLd != null) head += $"<script type=\"application/ld+json\">{page.JsonLd}</script>";
        // JsonSerializer conserva su escape HTML predeterminado para evitar cerrar el script.
        template = Regex.Replace(template, "<title>.*?</title>", _ => $"<title>{E(page.Title)}</title>", RegexOptions.Singleline);
        template = Regex.Replace(template, "<meta name=\"robots\" content=\"noindex, nofollow\"\\s*/?>", "");
        return template.Replace("</head>", head + "</head>");
    }

    public string Robots() => Indexable ? $"User-agent: *\nDisallow: /api/\nSitemap: {Origin}/sitemap.xml\n" : "User-agent: *\nDisallow: /\n";
    public async Task<string> SitemapAsync(int? page, CancellationToken ct)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        if (!Indexable) return new XDocument(new XElement(ns + "urlset")).ToString();
        if (page is null)
        {
            var count = await db.Products.CountAsync(p => p.IsActive, ct);
            var paths = Enumerable.Range(0, (count + 999) / 1000 + 1).Select(p => $"{Origin}/sitemaps/{p}.xml");
            return new XDocument(new XElement(ns + "sitemapindex", paths.Select(p => new XElement(ns + "sitemap", new XElement(ns + "loc", p))))).ToString();
        }
        var urls = page == 0 ? new List<string> { "/", "/comprar", "/informacion" } :
            (await db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Id).Skip((page.Value - 1) * 1000).Take(1000).Select(p => p.Id).ToListAsync(ct)).Select(id => $"/products/{id}").ToList();
        return new XDocument(new XElement(ns + "urlset", urls.Select(p => new XElement(ns + "url", new XElement(ns + "loc", Origin + p))))).ToString();
    }
}
