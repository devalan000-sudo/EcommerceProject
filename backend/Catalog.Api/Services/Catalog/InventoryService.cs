using System.ComponentModel.DataAnnotations;
using System.Data;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class InventoryService(CatalogDbContext db)
{
    public Task<List<VariantResponseDto>> ListAsync(int productId, bool admin, CancellationToken ct) =>
        Query(db, productId, admin, DateTime.UtcNow).ToListAsync(ct);
    public static IQueryable<VariantResponseDto> Query(CatalogDbContext db, int productId, bool admin, DateTime now) =>
        db.ProductVariants.AsNoTracking().Where(v => v.ProductId == productId && (admin || (v.IsActive && v.Product.IsActive)))
            .OrderBy(v => v.Size).ThenBy(v => v.Color)
            .Select(v => new VariantResponseDto(v.Id, v.Size, v.Color, v.Stock,
                v.Stock - (v.Reservations.Where(r => !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold)).Sum(r => (int?)r.Quantity) ?? 0), v.IsActive, v.ImageUrl));

    public async Task<VariantResponseDto> SaveVariantAsync(int productId, int? variantId, VariantWriteDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct)) throw new ValidationException("Producto no disponible.");
        var size = VariantTemplateService.NormalizeSize(dto.Size); var color = dto.Color.Trim().ToUpperInvariant();
        var existingVariants = await db.ProductVariants.Where(v => v.ProductId == productId).ToListAsync(ct);
        if (existingVariants.Any(v => VariantTemplateService.NormalizeSize(v.Size) == size && v.Color.Trim().Equals(color, StringComparison.OrdinalIgnoreCase) && v.Id != variantId))
            throw new ValidationException("Ya existe esa combinación de talla y color.");
        var variant = variantId.HasValue ? await db.ProductVariants.SingleOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId, ct)
            : new ProductVariant { ProductId = productId };
        if (variant is null) throw new ValidationException("Variante no disponible.");
        if (variantId.HasValue && VariantTemplateService.NormalizeSize(variant.Size) == size) size = variant.Size;
        if (variantId.HasValue && (VariantTemplateService.NormalizeSize(variant.Size) != VariantTemplateService.NormalizeSize(size) || variant.Color != color) && await db.OrderItems.AnyAsync(i => i.VariantId == variant.Id, ct))
            throw new ValidationException("Esta variante tiene pedidos. Conserva su talla y color y crea una combinación nueva para mantener el historial.");
        var now = DateTime.UtcNow;
        var held = await db.InventoryReservations.Where(r => r.VariantId == variant.Id && !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold)).SumAsync(r => (int?)r.Quantity, ct) ?? 0;
        if (dto.Stock < held || (held > 0 && (!dto.IsActive || size != variant.Size || color != variant.Color)))
            throw new ValidationException("Hay unidades reservadas. No puedes reducirlas ni cambiar o desactivar esa combinación hasta que termine la reserva.");
        var imageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim();
        var siblings = await db.ProductVariants.Where(v => v.ProductId == productId && v.Color == color).ToListAsync(ct);
        if (!variantId.HasValue && imageUrl is null) imageUrl = siblings.FirstOrDefault(v => v.ImageUrl != null)?.ImageUrl;
        foreach (var sibling in siblings) sibling.ImageUrl = imageUrl;
        variant.ImageUrl = imageUrl;
        variant.Size = size; variant.Color = color; variant.Stock = dto.Stock; variant.IsActive = dto.IsActive;
        if (!variantId.HasValue) db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(variant.Id, size, color, variant.Stock, variant.Stock - held, variant.IsActive, variant.ImageUrl);
    }

    // Lo invocará el inicio del pago; agregar al carrito y crear el pedido NO reservan.
    // SERIALIZABLE protege las lecturas de stock y reservas frente a escrituras concurrentes.
    public async Task<DateTime> ReserveForPaymentAsync(Guid orderId, int customerId, CancellationToken ct, bool stripeHold = false, string? frontendUrl = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
        if (order is null || order.CancellationPending || order.PaymentMethod != PaymentMethod.Card || order.PaymentStatus != PaymentStatus.Pending ||
            order.FulfillmentStatus != FulfillmentStatus.PendingConfirmation || order.Total is null || order.Items.Count == 0)
            throw new ValidationException("El pedido no está listo para pagar con tarjeta.");
        var now = DateTime.UtcNow;
        var previous = await db.InventoryReservations.Where(r => r.OrderId == orderId).ToListAsync(ct);
        if (previous.Count > 0)
        {
            if (previous.Any(r => r.Released || r.ExpiresAtUtc <= now)) throw new ValidationException("La reserva venció. Crea un pedido nuevo para volver a comprobar disponibilidad.");
            if (stripeHold) { order.StripeHold = true; order.PaymentDeadlineUtc = previous.Min(r => r.ExpiresAtUtc); order.StripeFrontendUrl = frontendUrl; await db.SaveChangesAsync(ct); }
            await tx.CommitAsync(ct);
            return previous.Min(r => r.ExpiresAtUtc); // Reintentar no prolonga la reserva.
        }
        var expires = now.AddMinutes(15);
        var reservations = new List<InventoryReservation>();
        foreach (var item in order.Items.OrderBy(i => i.VariantId))
        {
            var variant = await db.ProductVariants.SingleOrDefaultAsync(v => v.Id == item.VariantId && v.ProductId == item.ProductId && v.IsActive && v.Product.IsActive, ct);
            if (variant is null) throw new ValidationException("Una variante ya no está disponible.");
            var held = await db.InventoryReservations.Where(r => r.VariantId == variant.Id && !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold)).SumAsync(r => (int?)r.Quantity, ct) ?? 0;
            if (variant.Stock - held < item.Quantity) throw new ValidationException("Ya no hay suficientes unidades para este pedido.");
            reservations.Add(new InventoryReservation { OrderId = order.Id, VariantId = variant.Id, Quantity = item.Quantity, ExpiresAtUtc = expires });
        }
        db.InventoryReservations.AddRange(reservations);
        if (stripeHold) { order.StripeHold = true; order.PaymentDeadlineUtc = expires; order.StripeFrontendUrl = frontendUrl; }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return expires;
    }

    public Task ReleaseAsync(Guid orderId, CancellationToken ct) => db.InventoryReservations
        .Where(r => r.OrderId == orderId && !r.Released).ExecuteUpdateAsync(u => u.SetProperty(r => r.Released, true), ct);
}
