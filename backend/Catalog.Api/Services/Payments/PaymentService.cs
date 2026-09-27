using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Catalog.Api.Data;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public record PaymentSessionDto(string? Url, DateTime? DeadlineUtc, string Status);

public class PaymentService(CatalogDbContext db, InventoryService inventory, StripeGateway stripe)
{
    public async Task<PaymentSessionDto> StartAsync(Guid orderId, int customerId, CancellationToken ct)
    {
        stripe.RequireConfigured();
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
        if (order is null || order.PaymentMethod != PaymentMethod.Card || order.Total is null || order.Total <= 0 || order.Total > 999999.99m)
            throw new ValidationException("El pedido no está listo para Stripe o supera el límite de importe permitido.");
        if (order.CancellationPending || order.PaymentStatus != PaymentStatus.Pending || order.FulfillmentStatus != FulfillmentStatus.PendingConfirmation)
            throw new ValidationException("El pedido ya no está pendiente de pago.");
        if (!order.StripeHold)
            await inventory.ReserveForPaymentAsync(order.Id, customerId, ct, stripeHold: true, frontendUrl: stripe.FrontendUrl());
        db.ChangeTracker.Clear();
        order = await db.Orders.SingleAsync(o => o.Id == orderId, ct);
        var session = order.StripeSessionId is null ? await CreateRecoverableAsync(order, ct) : await stripe.GetAsync(order.StripeSessionId, ct);
        if ((order.CancellationPending || order.PaymentDeadlineUtc <= DateTime.UtcNow) && session.GetProperty("status").GetString() == "open")
            session = await ExpireOrRetrieveAsync(session.GetProperty("id").GetString()!, ct);
        await ApplySessionAsync(order.Id, session, ct);
        return new(session.GetProperty("status").GetString() == "open" ? session.GetProperty("url").GetString() : null,
            order.PaymentDeadlineUtc, session.GetProperty("status").GetString()!);
    }

    private async Task<JsonElement> CreateRecoverableAsync(Order order, CancellationToken ct)
    {
        // Tras una respuesta incierta se usa exactamente la misma clave; nunca se crea otro intento.
        // Stripe retiene claves al menos 24 h; fuera de esa ventana se requiere revisión manual.
        if (order.PaymentDeadlineUtc < DateTime.UtcNow.AddHours(-23))
            throw new ValidationException("Pago pendiente de revisión por el negocio. No se creará otra sesión.");
        var session = await stripe.CreateAsync(order, ct);
        var id = session.GetProperty("id").GetString();
        await db.Orders.Where(o => o.Id == order.Id && o.StripeSessionId == null).ExecuteUpdateAsync(u => u.SetProperty(o => o.StripeSessionId, id), ct);
        return session;
    }

    private async Task<JsonElement> ExpireOrRetrieveAsync(string id, CancellationToken ct)
    {
        try { return await stripe.ExpireAsync(id, ct); }
        catch (HttpRequestException) { return await stripe.GetAsync(id, ct); } // Puede haber completado el pago a la vez.
    }

    public async Task CancelAsync(Guid orderId, int customerId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
        if (order is null || !order.StripeHold) throw new ValidationException("No hay una sesión pendiente para cancelar.");
        var session = order.StripeSessionId is null ? await CreateRecoverableAsync(order, ct) : await stripe.GetAsync(order.StripeSessionId, ct);
        if (session.GetProperty("status").GetString() == "open") session = await ExpireOrRetrieveAsync(session.GetProperty("id").GetString()!, ct);
        await ApplySessionAsync(orderId, session, ct);
    }

    public async Task ReconcileAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId && o.StripeHold, ct);
        if (order is null) return;
        var session = order.StripeSessionId is null ? await CreateRecoverableAsync(order, ct) : await stripe.GetAsync(order.StripeSessionId, ct);
        if ((order.CancellationPending || order.PaymentDeadlineUtc <= DateTime.UtcNow) && session.GetProperty("status").GetString() == "open")
            session = await ExpireOrRetrieveAsync(session.GetProperty("id").GetString()!, ct);
        await ApplySessionAsync(orderId, session, ct);
    }

    // Solo recibe objetos autenticados: respuesta directa de Stripe o webhook con firma válida.
    public async Task ApplySessionAsync(Guid orderId, JsonElement session, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) throw new ValidationException("Pedido Stripe desconocido.");
        var id = session.GetProperty("id").GetString();
        if (session.GetProperty("livemode").GetBoolean() || session.GetProperty("mode").GetString() != "payment" ||
            session.GetProperty("client_reference_id").GetString() != orderId.ToString() ||
            session.GetProperty("metadata").GetProperty("order_id").GetString() != orderId.ToString() ||
            session.GetProperty("currency").GetString() != "mxn" || session.GetProperty("amount_total").GetInt64() != order.Total * 100 ||
            (order.StripeSessionId is not null && order.StripeSessionId != id) || order.PaymentMethod != PaymentMethod.Card)
            throw new ValidationException("La sesión de Stripe no coincide con el pedido.");
        if (order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded) { await tx.CommitAsync(ct); return; }
        if (order.PaymentStatus == PaymentStatus.Failed && session.GetProperty("status").GetString() == "expired") { await tx.CommitAsync(ct); return; }
        if (!order.StripeHold) throw new ValidationException("El pedido no tiene una reserva Stripe válida.");
        order.StripeSessionId = id;
        var reservations = await db.InventoryReservations.Where(r => r.OrderId == order.Id).OrderBy(r => r.VariantId).ToListAsync(ct);
        if (session.GetProperty("status").GetString() == "complete" && session.GetProperty("payment_status").GetString() == "paid")
        {
            if (reservations.Count == 0 || reservations.Any(r => r.Released)) throw new ValidationException("Pago pendiente de revisión de inventario.");
            foreach (var reservation in reservations)
            {
                var variant = await db.ProductVariants.SingleAsync(v => v.Id == reservation.VariantId, ct);
                if (variant.Stock < reservation.Quantity) throw new ValidationException("Inventario inconsistente; requiere revisión.");
                variant.Stock -= reservation.Quantity; reservation.Released = true;
            }
            order.PaymentStatus = PaymentStatus.Paid; order.StripeHold = false;
            order.CancellationPending = false; order.FulfillmentVersion = Guid.NewGuid();
        }
        else if (session.GetProperty("status").GetString() == "expired")
        {
            foreach (var reservation in reservations) reservation.Released = true;
            order.StripeHold = false; order.PaymentStatus = PaymentStatus.Failed;
            order.FulfillmentStatus = FulfillmentStatus.Cancelled;
            order.CancellationPending = false; order.FulfillmentVersion = Guid.NewGuid();
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
