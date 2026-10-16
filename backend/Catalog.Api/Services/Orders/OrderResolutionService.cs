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

public class OrderResolutionService(CatalogDbContext db, OrderService orders, PaymentService payments, StripeGateway stripe)
{
    // Solicita o completa la cancelación de un pedido sin pago confirmado.
    public async Task<OrderResponseDto?> CancelAsync(Guid id, int adminId, CancelOrderDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        bool pending;
        using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id, ct);
            if (order is null) return null;
            if (order.FulfillmentStatus == FulfillmentStatus.Cancelled) return await orders.GetAsync(id, null, ct);
            if (order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded || order.RefundRequestId.HasValue)
                throw new ValidationException("El pedido tiene un pago. Utiliza el reembolso en lugar de cancelarlo sin devolución.");
            EnsureBeforeShipping(order);
            if (!order.CancellationPending)
            {
                if (dto.Version != order.FulfillmentVersion) throw new DbUpdateConcurrencyException();
                order.CancellationReason = dto.Reason.Trim(); order.CancellationAdminId = adminId;
                order.CancellationRequestedAtUtc = DateTime.UtcNow;
            }
            pending = order.StripeHold;
            if (!pending && order.StripeSessionId is not null && order.PaymentStatus == PaymentStatus.Pending)
                throw new ValidationException("La sesión Stripe requiere revisión antes de cancelar.");
            order.CancellationPending = pending;
            if (!pending)
            {
                order.FulfillmentStatus = FulfillmentStatus.Cancelled; order.PaymentStatus = PaymentStatus.Failed;
                var reservations = await db.InventoryReservations.Where(r => r.OrderId == id && !r.Released).ToListAsync(ct);
                foreach (var reservation in reservations) reservation.Released = true;
            }
            order.FulfillmentVersion = Guid.NewGuid();
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        // El worker también recupera la cancelación si se pierde la conexión en este punto.
        if (pending) { db.ChangeTracker.Clear(); await payments.ReconcileAsync(id, ct); }
        return await orders.GetAsync(id, null, ct);
    }

    // Solicita un reembolso total, ya sea mediante Stripe o manualmente.
    public async Task<OrderResponseDto?> RefundAsync(Guid id, int adminId, RefundOrderDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))));
        bool isStripe;
        using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct);
            if (order is null) return null;
            isStripe = order.PaymentMethod == PaymentMethod.Card;
            if (order.RefundRequestId.HasValue)
            {
                if (order.RefundRequestId != dto.RequestId || order.RefundRequestHash != hash)
                    throw new ValidationException("Ya existe una solicitud de reembolso. Actualiza el detalle para consultar su resultado.");
            }
            else
            {
                ValidateRefundEligibility(order, dto, isStripe);
                if (dto.Restock) await RestockAsync(order, false, ct);
                ApplyRefundRequest(order, dto, adminId, hash);
                if (!isStripe) await CompleteAsync(order, ct);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        if (isStripe) await ReconcileRefundAsync(id, ct);
        return await orders.GetAsync(id, null, ct);
    }

    private void ValidateRefundEligibility(Order order, RefundOrderDto dto, bool isStripe)
    {
        if (order.PaymentStatus != PaymentStatus.Paid || order.Total is null || order.Total <= 0 ||
            order.StripeHold || order.CancellationPending)
            throw new ValidationException("Solo se reembolsan pedidos pagados y conciliados.");
        EnsureBeforeShipping(order);
        if (order.FulfillmentStatus == FulfillmentStatus.Cancelled)
            throw new ValidationException("El pedido ya está cancelado.");
        if (dto.Version != order.FulfillmentVersion)
            throw new DbUpdateConcurrencyException();

        if (isStripe)
        {
            stripe.RequireConfigured();
            if (order.StripeSessionId is null)
                throw new ValidationException("Falta la sesión del pago Stripe.");
        }
        else if (!dto.ManualRefundConfirmed || order.ManualPaymentConfirmedAtUtc is null)
        {
            throw new ValidationException("Confirma que devolviste el total por fuera del sistema. El pago original debe estar registrado.");
        }
    }

    private static void ApplyRefundRequest(Order order, RefundOrderDto dto, int adminId, string hash)
    {
        order.RefundRequestId = dto.RequestId;
        order.RefundRequestHash = hash;
        order.RefundReason = dto.Reason.Trim();
        order.RefundAdminId = adminId;
        order.RefundRequestedAtUtc = DateTime.UtcNow;
        order.RefundRestock = dto.Restock;
        order.RefundStatus = "Pending";
        order.FulfillmentVersion = Guid.NewGuid();
    }

    // Consulta Stripe y continúa un reembolso pendiente de una operación anterior.
    public async Task ReconcileRefundAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null || order.PaymentMethod != PaymentMethod.Card || !order.RefundRequestId.HasValue || order.RefundStatus == "Succeeded") return;
        stripe.RequireConfigured();
        var result = await GetRefundResultAsync(order, id, ct);
        if (result is not null) await ApplyRefundAsync(id, result.Value, ct);
    }

    private async Task<JsonElement?> GetRefundResultAsync(Order order, Guid orderId, CancellationToken ct)
    {
        if (order.StripeRefundId is not null)
            return await stripe.GetRefundAsync(order.StripeRefundId, ct);

        if (order.RefundRequestedAtUtc < DateTime.UtcNow.AddHours(-23))
        {
            await db.Orders.Where(o => o.Id == orderId && o.StripeRefundId == null)
                .ExecuteUpdateAsync(u => u.SetProperty(o => o.RefundStatus, "NeedsReview"), ct);
            return null; // No recrear fuera de la ventana segura de idempotencia.
        }

        var session = await stripe.GetAsync(order.StripeSessionId!, ct);
        ValidateStripePaymentSession(session, order, orderId);
        var intent = session.GetProperty("payment_intent").GetString();
        if (string.IsNullOrWhiteSpace(intent) ||
            (order.RefundPaymentIntentId is not null && order.RefundPaymentIntentId != intent))
            throw new ValidationException("El identificador de pago no coincide.");

        await db.Orders.Where(o => o.Id == orderId && o.RefundPaymentIntentId == null)
            .ExecuteUpdateAsync(u => u.SetProperty(o => o.RefundPaymentIntentId, intent), ct);
        order.RefundPaymentIntentId = intent;
        return await stripe.RefundAsync(order, ct);
    }

    private static void ValidateStripePaymentSession(JsonElement session, Order order, Guid orderId)
    {
        if (session.GetProperty("id").GetString() != order.StripeSessionId ||
            session.GetProperty("status").GetString() != "complete" ||
            session.GetProperty("payment_status").GetString() != "paid" ||
            session.GetProperty("amount_total").GetInt64() != order.Total * 100 ||
            session.GetProperty("currency").GetString() != "mxn" ||
            session.GetProperty("client_reference_id").GetString() != orderId.ToString() ||
            session.GetProperty("metadata").GetProperty("order_id").GetString() != orderId.ToString())
            throw new ValidationException("La sesión no coincide con el pago a reembolsar.");
    }

    // Aplica una respuesta autenticada de reembolso recibida directamente de Stripe.
    public async Task ApplyRefundAsync(Guid id, JsonElement refund, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == id, ct);
        var refundId = refund.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(refundId) || order.PaymentMethod != PaymentMethod.Card || !order.RefundRequestId.HasValue ||
            refund.GetProperty("object").GetString() != "refund" || refund.GetProperty("currency").GetString() != "mxn" ||
            refund.GetProperty("amount").GetInt64() != order.Total * 100 ||
            refund.GetProperty("payment_intent").GetString() != order.RefundPaymentIntentId || order.RefundPaymentIntentId is null ||
            refund.GetProperty("metadata").GetProperty("order_id").GetString() != id.ToString() ||
            refund.GetProperty("metadata").GetProperty("request_id").GetString() != order.RefundRequestId.ToString() ||
            (order.StripeRefundId is not null && order.StripeRefundId != refundId) ||
            (refund.TryGetProperty("livemode", out var live) && live.GetBoolean()))
            throw new ValidationException("El reembolso no coincide con la solicitud.");
        if (order.RefundStatus == "Succeeded") return;
        order.StripeRefundId = refundId;
        var status = refund.GetProperty("status").GetString();
        order.RefundStatus = status switch { "succeeded" => "Succeeded", "failed" or "canceled" => "Failed", "requires_action" => "NeedsReview", _ => "Pending" };
        if (status == "succeeded") await CompleteAsync(order, ct);
        order.FulfillmentVersion = Guid.NewGuid();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    // Marca el reembolso como terminado y, si se pidió, intenta reponer el inventario.
    private async Task CompleteAsync(Order order, CancellationToken ct)
    {
        // Confirmar el dinero aun si el catálogo cambió: la reposición queda para revisión separada.
        if (order.RefundRestock && order.RestockedAtUtc is null)
        {
            try { await RestockAsync(order, true, ct); }
            catch (ValidationException) { /* Refunded + RestockedAtUtc null indica reposición pendiente. */ }
        }
        order.RefundStatus = "Succeeded"; order.PaymentStatus = PaymentStatus.Refunded;
        order.RefundedAtUtc = DateTime.UtcNow; order.FulfillmentStatus = FulfillmentStatus.Cancelled;
    }

    // Reintenta una reposición que quedó pendiente después de confirmar el reembolso.
    public async Task<OrderResponseDto?> RetryRestockAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return null;
        if (order.PaymentStatus != PaymentStatus.Refunded || !order.RefundRestock) throw new ValidationException("El pedido no tiene una reposición pendiente autorizada.");
        if (order.RestockedAtUtc is null) { await RestockAsync(order, true, ct); order.FulfillmentVersion = Guid.NewGuid(); await db.SaveChangesAsync(ct); }
        await tx.CommitAsync(ct); return await orders.GetAsync(id, null, ct);
    }

    // Valida los artículos y calcula los ajustes de stock de una devolución.
    private async Task RestockAsync(Order order, bool apply, CancellationToken ct)
    {
        if (order.Items.Count == 0 || order.Items.Any(i => i.VariantId is null || i.ProductId is null || i.Quantity <= 0))
            throw new ValidationException("Hay artículos sin variante para reponer.");
        var variantIds = order.Items.Select(item => item.VariantId!.Value).Distinct().ToArray();
        var variants = await db.ProductVariants.Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);
        var adjustments = new List<(ProductVariant Variant, int Quantity)>();
        foreach (var group in order.Items.GroupBy(i => i.VariantId!.Value).OrderBy(g => g.Key))
        {
            variants.TryGetValue(group.Key, out var variant);
            var quantity = group.Sum(i => i.Quantity);
            if (variant is null || group.Any(i => i.ProductId != variant.ProductId || i.Size != variant.Size || i.Color != variant.Color) || variant.Stock > 1000000 - quantity)
                throw new ValidationException("Una variante cambió o no admite la reposición. Revisa el inventario.");
            adjustments.Add((variant, quantity));
        }
        if (!apply) return;
        foreach (var (variant, quantity) in adjustments) variant.Stock += quantity;
        order.RestockedAtUtc = DateTime.UtcNow;
    }
    // Las cancelaciones y reembolsos de este servicio solo aplican antes del envío.
    private static void EnsureBeforeShipping(Order order)
    {
        if (order.FulfillmentStatus is FulfillmentStatus.Shipped or FulfillmentStatus.Delivered)
            throw new ValidationException("Este pedido ya salió. Las devoluciones posteriores a la entrega requieren un flujo de devolución física, aún no disponible.");
    }
}
