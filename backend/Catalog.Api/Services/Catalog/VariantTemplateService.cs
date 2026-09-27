using System.ComponentModel.DataAnnotations;
using System.Data;
using Catalog.Api.Data;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class VariantTemplateRequest
{
    [Required, StringLength(50)] public string MainColor { get; set; } = "Original";
    [Range(0, 1000000)] public int InitialStock { get; set; }
}

public class VariantTemplateService(CatalogDbContext db)
{
    // Colores revisados visualmente de los 32 recursos originales de la plantilla.
    private static readonly string[] SampleColors = ["BEIGE", "AZUL MARINO", "BLANCO", "AZUL", "BLANCO", "GRIS",
        "NEGRO", "BEIGE", "VERDE", "VINO", "AZUL MARINO", "BLANCO / NEGRO", "NEGRO", "CAMEL", "VERDE", "VINO / BEIGE",
        "GRIS", "NEGRO", "BEIGE", "AZUL MARINO", "TERRACOTA", "VERDE / BEIGE", "GRIS", "NEGRO", "VERDE", "AZUL", "VINO",
        "NEGRO", "AZUL", "AZUL MARINO", "BEIGE", "GRIS"];

    public static string NormalizeSize(string size) => size.Trim().ToUpperInvariant() switch
    {
        "CHICA" or "CH" or "S" => "S",
        "MEDIANA" or "M" => "M",
        "GRANDE" or "G" or "L" => "L",
        var value => value
    };

    public async Task<int> ApplyAsync(int productId, VariantTemplateRequest request, CancellationToken ct)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var product = await db.Products.Include(p => p.Variants).SingleOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new ValidationException("Producto no disponible.");
        var main = request.MainColor.Trim().ToUpperInvariant();
        var colors = new[] { main, "NEGRO", "BLANCO", "AZUL", "GRIS" }.Distinct().Take(4).ToArray();
        var added = 0;
        foreach (var color in colors)
            foreach (var size in new[] { "S", "M", "L" })
            {
                if (product.Variants.Any(v => NormalizeSize(v.Size) == size && v.Color.Trim().Equals(color, StringComparison.OrdinalIgnoreCase))) continue;
                // No sobrescribir existencias, IDs, reservas ni fotografías de variantes anteriores.
                var knownImage = product.Variants.FirstOrDefault(v => v.Color.Trim().Equals(color, StringComparison.OrdinalIgnoreCase) && v.ImageUrl != null)?.ImageUrl;
                db.ProductVariants.Add(new ProductVariant
                {
                    ProductId = productId,
                    Size = size,
                    Color = color,
                    Stock = request.InitialStock,
                    IsActive = true,
                    ImageUrl = knownImage ?? (color == main ? product.ImageUrl : null)
                });
                added++;
            }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return added;
    }

    public async Task<int> ApplyCatalogAsync(int initialStock, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().Select(p => new { p.Id, p.Name, p.ImageUrl }).ToListAsync(ct);
        var total = 0;
        foreach (var product in products)
        {
            // Usar sólo colores explícitos del nombre; no deducir el contenido de una foto por su URL.
            var words = product.Name.ToUpperInvariant().Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
            var main = new[] { "AZUL", "NEGRO", "BLANCO", "GRIS", "ROJO", "VERDE", "BEIGE" }.FirstOrDefault(words.Contains) ?? "ORIGINAL";
            if (Uri.TryCreate(product.ImageUrl, UriKind.Absolute, out var image) && image.IsLoopback &&
                image.AbsolutePath.StartsWith("/images/products/") && Path.GetExtension(image.AbsolutePath) == ".png" &&
                int.TryParse(Path.GetFileNameWithoutExtension(image.AbsolutePath), out var imageId) && imageId is >= 1001 and <= 1032)
                main = SampleColors[imageId - 1001];
            total += await ApplyAsync(product.Id, new() { MainColor = main, InitialStock = initialStock }, ct);
            db.ChangeTracker.Clear();
        }
        return total;
    }
}
