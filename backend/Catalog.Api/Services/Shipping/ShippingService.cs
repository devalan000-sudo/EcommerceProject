using System.ComponentModel.DataAnnotations;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class ShippingService(CatalogDbContext db)
{
    public Task<List<ShippingResponseDto>> ListAsync(bool admin, CancellationToken ct) => db.ShippingOptions.AsNoTracking()
        .Where(s => admin || s.IsActive).OrderBy(s => s.Id)
        .Select(s => new ShippingResponseDto(s.Id, s.Name, s.Description, s.Kind, s.Price, s.IsActive)).ToListAsync(ct);

    public async Task<ShippingResponseDto?> SaveAsync(int? id, ShippingWriteDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        var option = id.HasValue ? await db.ShippingOptions.FindAsync([id.Value], ct) : new ShippingOption();
        if (option is null) return null;
        option.Name = dto.Name.Trim(); option.Description = dto.Description; option.Kind = dto.Kind;
        option.Price = dto.Price; option.IsActive = dto.IsActive;
        if (!id.HasValue) db.ShippingOptions.Add(option);
        await db.SaveChangesAsync(ct);
        return new(option.Id, option.Name, option.Description, option.Kind, option.Price, option.IsActive);
    }
}
