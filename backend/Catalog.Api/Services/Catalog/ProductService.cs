using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class ProductService(CatalogDbContext db)
{
    private async Task<List<ProductResponseDto>> ReadAsync(bool includeInactive, int? id, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => (includeInactive || p.IsActive) && (id == null || p.Id == id))
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new ProductResponseDto(p.Id, p.Name, p.Description, p.Price,
                p.ImageUrl, p.IsActive, p.CategoryId, p.Category.Name, p.ViewCount, null, true)).ToListAsync(ct);
        if (products.Count == 0) return products;
        var productIds = products.Select(p => p.Id).ToArray();
        var variants = await db.ProductVariants.AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId) && (includeInactive || v.IsActive))
            .OrderBy(v => v.Id).Select(v => new { v.Id, v.ProductId, v.Size, v.Color, v.Stock, v.IsActive, v.ImageUrl }).ToListAsync(ct);
        var now = DateTime.UtcNow;
        // Aggregate once instead of running a correlated SUM for every catalog variant.
        var reserved = await db.InventoryReservations.AsNoTracking()
            .Where(r => !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold) && productIds.Contains(r.Variant.ProductId))
            .GroupBy(r => r.VariantId).Select(g => new { Id = g.Key, Quantity = g.Sum(r => r.Quantity) })
            .ToDictionaryAsync(r => r.Id, r => r.Quantity, ct);
        var grouped = variants.ToLookup(v => v.ProductId);
        return products.Select(p => p with
        {
            Variants = grouped[p.Id].Select(v => new VariantResponseDto(
            v.Id, v.Size, v.Color, v.Stock, v.Stock - reserved.GetValueOrDefault(v.Id), v.IsActive, v.ImageUrl)).ToList()
        }).ToList();
    }

    // Incremento atómico: las visitas simultáneas no sobrescriben el contador.
    public async Task<bool> RegisterViewAsync(int id, CancellationToken ct) =>
        await db.Products.Where(p => p.Id == id && p.IsActive)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), ct) > 0;

    public Task<List<ProductResponseDto>> ListAsync(bool includeInactive, CancellationToken ct) =>
        ReadAsync(includeInactive, null, ct);

    public async Task<ProductResponseDto?> GetAsync(int id, bool includeInactive, CancellationToken ct) =>
        (await ReadAsync(includeInactive, id, ct)).FirstOrDefault();

    public Task<bool> CategoryExistsAsync(int id, CancellationToken ct) => db.Categories.AnyAsync(c => c.Id == id, ct);

    public async Task<ProductResponseDto> CreateAsync(ProductCreateDto dto, CancellationToken ct)
    {
        var product = new Product();
        Apply(product, dto);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(product.Id, true, ct))!;
    }

    public async Task<bool> UpdateAsync(int id, ProductUpdateDto dto, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return false;
        if (dto.IsActive && !await db.ProductVariants.AnyAsync(v => v.ProductId == id && v.IsActive && v.Size.Trim() != "" && v.Color.Trim() != "", ct))
            throw new System.ComponentModel.DataAnnotations.ValidationException("Antes de publicar, configura al menos una variante activa con talla, color y existencias en Inventario. Puedes usar Talla única y Color único cuando corresponda.");
        Apply(product, dto);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        if (await db.InventoryReservations.AnyAsync(r => r.Variant.ProductId == id, ct))
            throw new System.ComponentModel.DataAnnotations.ValidationException("Este producto tiene historial de reservas. Desactívalo para conservar la trazabilidad.");
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return false;
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(Product p, ProductCreateDto dto)
    {
        p.Name = dto.Name.Trim();
        p.Description = dto.Description;
        p.Price = dto.Price;
        p.ImageUrl = dto.ImageUrl;
        p.IsActive = dto.IsActive;
        p.CategoryId = dto.CategoryId;
    }
}
