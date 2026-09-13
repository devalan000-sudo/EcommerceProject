using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class ProductService(CatalogDbContext db)
{
    private IQueryable<ProductResponseDto> Query(bool includeInactive, int? id = null) => db.Products.AsNoTracking()
        .Where(p => (includeInactive || p.IsActive) && (id == null || p.Id == id))
        .OrderBy(p => p.Name)
        .Select(p => new ProductResponseDto(p.Id, p.Name, p.Description, p.Price,
            p.ImageUrl, p.IsActive, p.CategoryId, p.Category.Name, p.ViewCount));

    // Incremento atómico: las visitas simultáneas no sobrescriben el contador.
    public async Task<bool> RegisterViewAsync(int id, CancellationToken ct) =>
        await db.Products.Where(p => p.Id == id && p.IsActive)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), ct) > 0;

    public Task<List<ProductResponseDto>> ListAsync(bool includeInactive, CancellationToken ct) =>
        Query(includeInactive).ToListAsync(ct);

    public Task<ProductResponseDto?> GetAsync(int id, bool includeInactive, CancellationToken ct) =>
        Query(includeInactive, id).FirstOrDefaultAsync(ct);

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
        Apply(product, dto);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
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
