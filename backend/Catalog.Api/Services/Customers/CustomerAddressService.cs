using System.ComponentModel.DataAnnotations;
using System.Data;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class CustomerAddressService(CatalogDbContext db)
{
    public async Task<List<CustomerAddressResponseDto>> ListAsync(int customerId, CancellationToken ct) =>
        (await db.CustomerAddresses.AsNoTracking().Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id).ToListAsync(ct)).Select(Response).ToList();

    public async Task<CustomerAddressResponseDto?> SaveAsync(int customerId, int? id, CustomerAddressWriteDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var addresses = db.CustomerAddresses.Where(a => a.CustomerId == customerId);
        var address = id.HasValue ? await addresses.SingleOrDefaultAsync(a => a.Id == id, ct) : new CustomerAddress { CustomerId = customerId };
        if (address is null) return null;
        if (id.HasValue && address.Version != dto.Version) throw new DbUpdateConcurrencyException();
        var count = await addresses.CountAsync(ct);
        if (!id.HasValue && count >= 10) throw new ValidationException("Puedes guardar hasta 10 direcciones. Edita o elimina una antes de añadir otra.");
        var makeDefault = dto.IsDefault || address.IsDefault || count == 0;
        if (makeDefault)
        {
            // Libera el índice único antes de asignar la nueva predeterminada; todo se confirma en la misma transacción.
            var demotedVersion = Guid.NewGuid();
            await addresses.Where(a => a.IsDefault && a.Id != address.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false).SetProperty(a => a.Version, demotedVersion), ct);
        }
        address.Label = dto.Label.Trim(); address.FirstName = dto.FirstName.Trim(); address.LastName = dto.LastName.Trim();
        address.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
        address.Street = dto.Street.Trim(); address.ExteriorNumber = dto.ExteriorNumber.Trim();
        address.InteriorNumber = dto.InteriorNumber?.Trim() ?? ""; address.Neighborhood = dto.Neighborhood.Trim();
        address.City = dto.City.Trim(); address.State = dto.State.Trim(); address.PostalCode = dto.PostalCode.Trim();
        address.Country = dto.Country.Trim(); address.References = dto.References?.Trim() ?? "";
        address.IsDefault = makeDefault; address.Version = Guid.NewGuid();
        if (!id.HasValue) db.CustomerAddresses.Add(address);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Response(address);
    }

    public async Task<bool> DeleteAsync(int customerId, int id, Guid? version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var address = await db.CustomerAddresses.SingleOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId, ct);
        if (address is null) return false;
        if (address.Version != version) throw new DbUpdateConcurrencyException();
        db.CustomerAddresses.Remove(address);
        await db.SaveChangesAsync(ct);
        if (address.IsDefault)
        {
            var next = await db.CustomerAddresses.Where(a => a.CustomerId == customerId).OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
            if (next is not null) { next.IsDefault = true; next.Version = Guid.NewGuid(); await db.SaveChangesAsync(ct); }
        }
        await tx.CommitAsync(ct);
        return true;
    }

    private static CustomerAddressResponseDto Response(CustomerAddress a) => new()
    {
        Id = a.Id,
        Label = a.Label,
        FirstName = a.FirstName,
        LastName = a.LastName,
        Phone = a.Phone,
        Street = a.Street,
        ExteriorNumber = a.ExteriorNumber,
        InteriorNumber = a.InteriorNumber,
        Neighborhood = a.Neighborhood,
        City = a.City,
        State = a.State,
        PostalCode = a.PostalCode,
        Country = a.Country,
        References = a.References,
        IsDefault = a.IsDefault,
        Version = a.Version
    };
}
