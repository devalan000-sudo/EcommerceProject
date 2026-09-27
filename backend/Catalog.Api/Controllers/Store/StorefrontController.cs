using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[AllowAnonymous]
public class StorefrontController(SeoService seo, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("robots.txt")]
    public IActionResult Robots() => Content(seo.Robots(), "text/plain; charset=utf-8");
    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct) => Content(await seo.SitemapAsync(null, ct), "application/xml; charset=utf-8");
    [HttpGet("sitemaps/{page:int:min(0):max(100000)}.xml")]
    public async Task<IActionResult> SitemapPage(int page, CancellationToken ct) => Content(await seo.SitemapAsync(page, ct), "application/xml; charset=utf-8");

    public async Task<IActionResult> Page(CancellationToken ct)
    {
        if (Request.Path.StartsWithSegments("/api")) return NotFound();
        var file = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "index.html");
        if (!System.IO.File.Exists(file)) file = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../frontend/dist/index.html"));
        if (!System.IO.File.Exists(file)) return StatusCode(503, "Compila el frontend para visualizar la tienda desde la API.");
        var page = await seo.PageAsync(Request.Path.Value ?? "/", ct);
        Response.Headers.CacheControl = "no-store";
        if (!seo.Indexable || !page.Public) Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return new ContentResult { StatusCode = page.Status, ContentType = "text/html; charset=utf-8", Content = seo.Render(await System.IO.File.ReadAllTextAsync(file, ct), page) };
    }
}
