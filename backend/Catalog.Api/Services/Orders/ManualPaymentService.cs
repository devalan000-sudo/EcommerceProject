using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class ManualPaymentService(CatalogDbContext db, OrderService orders)
{
    // Confirma un pago recibido fuera de Stripe y descuenta las existencias una sola vez.
    public async Task<OrderResponseDto?> ConfirmAsync(Guid id, int adminId, ManualPaymentDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        if (adminId <= 0) throw new ValidationException("Administrador inválido.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return null;
        if (order.PaymentMethod != PaymentMethod.WhatsApp || order.StripeHold || order.StripeSessionId is not null)
            throw new ValidationException("Esta confirmación solo admite pedidos de WhatsApp sin sesión Stripe.");
        if (order.ManualPaymentRequestId == dto.RequestId)
        {
            if (order.ManualPaymentRequestHash != hash) throw new ValidationException("La confirmación ya se utilizó con otros datos.");
            await tx.CommitAsync(ct);
            return await orders.GetAsync(id, null, ct);
        }
        if (order.PaymentStatus != PaymentStatus.Pending || order.ManualPaymentRequestId.HasValue)
            throw new ValidationException("El pedido ya tiene un pago registrado o no admite confirmación.");
        if (order.CancellationPending || order.RefundRequestId.HasValue) throw new ValidationException("El pedido tiene una cancelación o reembolso en curso.");
        if (order.FulfillmentStatus == FulfillmentStatus.Cancelled)
            throw new ValidationException("No se puede confirmar un pedido cancelado.");
        if (order.FulfillmentVersion != dto.Version) throw new DbUpdateConcurrencyException();
        // Pedidos históricos pudieron avanzar de entrega antes de existir la confirmación manual.
        // También requieren esta operación explícita; no se deducen existencias durante la migración.
        var shippingName = dto.ShippingName.Trim();
        if (order.ShippingCost.HasValue && (order.ShippingCost != dto.ShippingCost || order.ShippingName != shippingName))
            throw new ValidationException("La entrega ya estaba cotizada. Conserva su nombre e importe.");
        var total = order.Subtotal + dto.ShippingCost!.Value;
        if (dto.AmountReceived != total) throw new ValidationException("El importe recibido debe cubrir exactamente los productos y la entrega. No se admiten pagos parciales.");
        var now = DateTime.UtcNow;
        var adjustments = await CalculateStockAdjustmentsAsync(order, now, ct);
        ApplyManualPayment(order, dto, adminId, hash, shippingName, total, now, adjustments);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await orders.GetAsync(id, null, ct);
    }

    private async Task<List<(ProductVariant Variant, int Quantity)>> CalculateStockAdjustmentsAsync(
        Order order,
        DateTime now,
        CancellationToken ct)
    {
        if (order.Items.Count == 0 || order.Items.Any(i => !i.VariantId.HasValue || !i.ProductId.HasValue || i.Quantity <= 0))
            throw new ValidationException("El pedido contiene productos sin variante o eliminados. Crea un nuevo pedido con las tallas y colores acordados.");
        if (await db.InventoryReservations.AnyAsync(r => r.OrderId == order.Id && !r.Released, ct))
            throw new ValidationException("El pedido tiene reservas que requieren revisión antes de confirmar.");

        var variantIds = order.Items.Select(i => i.VariantId!.Value).Distinct().ToArray();
        var variants = await db.ProductVariants.Where(v => variantIds.Contains(v.Id) && v.IsActive && v.Product.IsActive)
            .ToDictionaryAsync(v => v.Id, ct);
        var heldByVariant = await db.InventoryReservations
            .Where(r => variantIds.Contains(r.VariantId) && !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold))
            .GroupBy(r => r.VariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(r => r.Quantity) })
            .ToDictionaryAsync(x => x.VariantId, x => x.Quantity, ct);

        var adjustments = new List<(ProductVariant Variant, int Quantity)>();
        foreach (var group in order.Items.GroupBy(i => i.VariantId!.Value).OrderBy(g => g.Key))
        {
            if (!variants.TryGetValue(group.Key, out var variant) ||
                group.Any(i => i.ProductId != variant.ProductId || i.Size != variant.Size || i.Color != variant.Color))
                throw new ValidationException("Una talla o color ya no coincide con el catálogo. Revisa el pedido antes de confirmar.");
            var quantity = group.Sum(i => i.Quantity);
            if (variant.Stock - heldByVariant.GetValueOrDefault(variant.Id) < quantity)
                throw new ValidationException("No hay suficientes existencias libres. Las unidades reservadas para otros pedidos no pueden utilizarse.");
            adjustments.Add((variant, quantity));
        }
        return adjustments;
    }

    private static void ApplyManualPayment(
        Order order,
        ManualPaymentDto dto,
        int adminId,
        string hash,
        string shippingName,
        decimal total,
        DateTime now,
        List<(ProductVariant Variant, int Quantity)> adjustments)
    {
        // Modificar solo tras validar todos los artículos evita cambios parciales.
        foreach (var (variant, quantity) in adjustments) variant.Stock -= quantity;
        order.ShippingName = shippingName; order.ShippingCost = dto.ShippingCost; order.Total = total;
        order.PaymentStatus = PaymentStatus.Paid;
        order.ManualPaymentRequestId = dto.RequestId; order.ManualPaymentRequestHash = hash;
        order.ManualPaymentConfirmedAtUtc = now; order.ManualPaymentAdminId = adminId;
        order.ManualPaymentMethod = dto.Method;
        order.ManualPaymentReference = string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim();
        order.FulfillmentVersion = Guid.NewGuid();
    }
}
