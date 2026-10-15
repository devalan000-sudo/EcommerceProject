using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class ProductService(CatalogDbContext db)
{
    private const string InactiveVariantValidationMessage = "Antes de publicar, configura al menos una variante activa con talla, color y existencias en Inventario. Puedes usar Talla única y Color único cuando corresponda.";
    private const string ReservationHistoryValidationMessage = "Este producto tiene historial de reservas. Desactívalo para conservar la trazabilidad.";

    // Lee productos y sus variantes. El catálogo público filtra inactivos y el administrador puede incluirlos.
    private async Task<List<ProductResponseDto>> ReadAsync(bool includeInactive, int? id, CancellationToken ct)
    {
        var products = await QueryProductsAsync(includeInactive, id, ct);
        if (products.Count == 0)
        {
            return products;
        }

        var productIds = products.Select(product => product.Id).ToArray();
        var variants = await QueryVariantsAsync(productIds, includeInactive, ct);
        var reservedStockDictionary = await QueryActiveReservationsAsync(productIds, ct);

        return AssembleProductResponses(products, variants, reservedStockDictionary);
    }

    private async Task<List<ProductResponseDto>> QueryProductsAsync(bool includeInactive, int? id, CancellationToken ct)
    {
        return await db.Products
            .AsNoTracking()
            .Where(product => (includeInactive || product.IsActive) && (id == null || product.Id == id))
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Select(product => new ProductResponseDto(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.ImageUrl,
                product.IsActive,
                product.CategoryId,
                product.Category.Name,
                product.ViewCount,
                null,
                true))
            .ToListAsync(ct);
    }

    private async Task<List<VariantInfo>> QueryVariantsAsync(int[] productIds, bool includeInactive, CancellationToken ct)
    {
        return await db.ProductVariants
            .AsNoTracking()
            .Where(variant => productIds.Contains(variant.ProductId) && (includeInactive || variant.IsActive))
            .OrderBy(variant => variant.Id)
            .Select(variant => new VariantInfo(
                variant.Id,
                variant.ProductId,
                variant.Size,
                variant.Color,
                variant.Stock,
                variant.IsActive,
                variant.ImageUrl))
            .ToListAsync(ct);
    }

    private async Task<Dictionary<int, int>> QueryActiveReservationsAsync(int[] productIds, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        return await db.InventoryReservations
            .AsNoTracking()
            .Where(reservation => !reservation.Released && (reservation.ExpiresAtUtc > now || reservation.Order.StripeHold) && productIds.Contains(reservation.Variant.ProductId))
            .GroupBy(reservation => reservation.VariantId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(reservation => reservation.Quantity) })
            .ToDictionaryAsync(item => item.Id, item => item.Quantity, ct);
    }

    private static List<ProductResponseDto> AssembleProductResponses(
        List<ProductResponseDto> products,
        List<VariantInfo> variants,
        Dictionary<int, int> reservedStockDictionary)
    {
        var groupedVariants = variants.ToLookup(variant => variant.ProductId);

        return products.Select(product => product with
        {
            Variants = groupedVariants[product.Id].Select(variant => new VariantResponseDto(
                variant.Id,
                variant.Size,
                variant.Color,
                variant.Stock,
                variant.Stock - reservedStockDictionary.GetValueOrDefault(variant.Id),
                variant.IsActive,
                variant.ImageUrl)).ToList()
        }).ToList();
    }

    // Incremento atómico: las visitas simultáneas no sobrescriben el contador.
    public async Task<bool> RegisterViewAsync(int id, CancellationToken ct) =>
        await db.Products
            .Where(product => product.Id == id && product.IsActive)
            .ExecuteUpdateAsync(update => update.SetProperty(product => product.ViewCount, product => product.ViewCount + 1), ct) > 0;

    // Devuelve todos los productos visibles para el tipo de usuario solicitado.
    public Task<List<ProductResponseDto>> ListAsync(bool includeInactive, CancellationToken ct) =>
        ReadAsync(includeInactive, null, ct);

    // Busca un producto concreto y devuelve null si no existe o no es visible.
    public async Task<ProductResponseDto?> GetAsync(int id, bool includeInactive, CancellationToken ct) =>
        (await ReadAsync(includeInactive, id, ct)).FirstOrDefault();

    // Comprueba que la categoría exista antes de asociarla a un producto.
    public Task<bool> CategoryExistsAsync(int id, CancellationToken ct) =>
        db.Categories.AnyAsync(category => category.Id == id, ct);

    // Crea un producto y devuelve su representación completa con variantes.
    public async Task<ProductResponseDto> CreateAsync(ProductCreateDto dto, CancellationToken ct)
    {
        var product = new Product();
        Apply(product, dto);
        
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        
        return (await GetAsync(product.Id, true, ct))!;
    }

    // Actualiza un producto. Publicarlo exige tener al menos una variante válida.
    public async Task<bool> UpdateAsync(int id, ProductUpdateDto dto, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null)
        {
            return false;
        }

        await EnsureCanBePublishedAsync(id, dto.IsActive, ct);

        Apply(product, dto);
        await db.SaveChangesAsync(ct);
        
        return true;
    }

    private async Task EnsureCanBePublishedAsync(int productId, bool isActive, CancellationToken ct)
    {
        if (isActive && !await db.ProductVariants.AnyAsync(variant =>
                variant.ProductId == productId && variant.IsActive &&
                variant.Size.Trim() != "" && variant.Color.Trim() != "", ct))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(InactiveVariantValidationMessage);
        }
    }

    // Elimina productos sin reservas; con historial, solo permite desactivarlos.
    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        if (await db.InventoryReservations.AnyAsync(reservation => reservation.Variant.ProductId == id, ct))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(ReservationHistoryValidationMessage);
        }

        var product = await db.Products.FindAsync([id], ct);
        if (product is null)
        {
            return false;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        
        return true;
    }

    // Copia los valores validados del DTO a la entidad persistente.
    private static void Apply(Product product, ProductCreateDto dto)
    {
        product.Name = dto.Name.Trim();
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.ImageUrl = dto.ImageUrl;
        product.IsActive = dto.IsActive;
        product.CategoryId = dto.CategoryId;
    }

    private record VariantInfo(int Id, int ProductId, string Size, string Color, int Stock, bool IsActive, string? ImageUrl);
}
