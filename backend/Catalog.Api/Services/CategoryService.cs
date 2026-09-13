using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace Catalog.Api.Services;

public class CategoryService(CatalogDbContext db)
{
    public Task<List<CategoryResponseDto>> ListAsync(CancellationToken ct) => db.Categories.AsNoTracking()
        .OrderBy(c => c.Name).Select(c => new CategoryResponseDto(c.Id, c.Name, c.Description)).ToListAsync(ct);

    public async Task<CategoryResponseDto> CreateAsync(CategoryWriteDto dto, CancellationToken ct)
    {
        var category = new Category { Name = dto.Name.Trim(), Description = dto.Description };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return new(category.Id, category.Name, category.Description);
    }

    public async Task<bool> UpdateAsync(int id, CategoryWriteDto dto, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([id], ct);
        if (category is null) return false;
        category.Name = dto.Name.Trim();
        category.Description = dto.Description;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string> DeleteAsync(int id, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([id], ct);
        if (category is null) return "missing";
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct)) return "in-use";
        db.Categories.Remove(category);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 }) { return "in-use"; }
        return "deleted";
    }
}
