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

public class ReturnRefundService(CatalogDbContext db, StripeGateway stripe)
{
    // Convierte un reembolso de devolución en la respuesta pública.
    public static ReturnRefundDto Response(ReturnRefund r) => new(r.Id, r.Amount, r.Status, r.Note, r.CreatedAtUtc, r.CompletedAtUtc,
        r.Items.OrderBy(i => i.OrderItemId).Select(i => new ReturnRefundItemDto(i.OrderItemId, i.Quantity, i.UnitPrice)).ToList());

    // Crea un reembolso parcial después de validar artículos recibidos y saldo disponible.
    public async Task<ReturnRefundDto?> CreateAsync(Guid returnId, int adminId, ReturnRefundCreateDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        if (dto.RequestId == Guid.Empty || dto.Items.Any(i => i is null || i.Quantity <= 0 || i.OrderItemId <= 0) || dto.Items.Select(i => i.OrderItemId).Distinct().Count() != dto.Items.Count)
            throw new ValidationException("Selecciona artículos distintos y cantidades positivas.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Note = dto.Note.Trim(),
            Reference = dto.ManualReference?.Trim() ?? "",
            dto.ManualRefundConfirmed,
            dto.ExpectedAmount,
            Items = dto.Items.OrderBy(i => i.OrderItemId).Select(i => new { i.OrderItemId, i.Quantity })
        }))));
        ReturnRefund refund;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var request = await db.ReturnRequests.Include(r => r.Order).Include(r => r.Items).ThenInclude(i => i.OrderItem)
                .Include(r => r.Refunds).ThenInclude(r => r.Items).SingleOrDefaultAsync(r => r.Id == returnId, ct);
            if (request is null) return null;
            var existing = request.Refunds.SingleOrDefault(r => r.ClientRequestId == dto.RequestId);
            if (existing is not null)
            {
                if (existing.RequestHash != hash) throw new ValidationException("La clave ya se utilizó con otros datos. Consulta el historial de reembolsos.");
                refund = existing;
            }
            else
            {
                if (request.Version != dto.Version) throw new DbUpdateConcurrencyException();
                var order = request.Order;
                if (request.Status is not (ReturnStatus.PartiallyReceived or ReturnStatus.Received) || order.PaymentStatus != PaymentStatus.Paid ||
                    order.Total is null or <= 0 || order.Currency != "MXN" || order.StripeHold || order.CancellationPending || order.RefundRequestId.HasValue || order.RefundStatus != null ||
                    order.FulfillmentStatus is not (FulfillmentStatus.Shipped or FulfillmentStatus.Delivered))
                    throw new ValidationException("Se requiere una devolución recibida y un pedido pagado sin otro conflicto de reembolso.");
                if (order.PaymentMethod == PaymentMethod.Card) { stripe.RequireConfigured(); if (order.StripeSessionId is null) throw new ValidationException("Falta la sesión del pago original."); }
                else if (!dto.ManualRefundConfirmed || string.IsNullOrWhiteSpace(dto.ManualReference) || order.ManualPaymentConfirmedAtUtc is null)
                    throw new ValidationException("Confirma el reembolso externo y registra su referencia. El pago original debe estar confirmado.");
                var lines = BuildRefundLines(request, dto);
                var amount = ValidateRefundAmount(lines, dto.ExpectedAmount);
                await ValidateRefundBalanceAsync(order, amount, ct);
                refund = new ReturnRefund
                {
                    ReturnRequest = request,
                    ClientRequestId = dto.RequestId,
                    RequestHash = hash,
                    AdminId = adminId,
                    Note = dto.Note.Trim(),
                    ManualReference = dto.ManualReference?.Trim() ?? "",
                    Amount = amount,
                    Items = lines
                };
                if (order.PaymentMethod == PaymentMethod.WhatsApp) { refund.Status = ReturnRefundStatus.Succeeded; refund.CompletedAtUtc = DateTime.UtcNow; }
                db.ReturnRefunds.Add(refund); request.Version = Guid.NewGuid(); order.FulfillmentVersion = Guid.NewGuid();
                if (refund.Status == ReturnRefundStatus.Succeeded) AfterSalesEmails.Queue(db, order, "ReturnRefundSucceeded", refund.Id);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        if (refund.ReturnRequest.Order.PaymentMethod == PaymentMethod.Card) await ReconcileAsync(refund.Id, null, ct);
        db.ChangeTracker.Clear();
        return Response(await db.ReturnRefunds.AsNoTracking().Include(r => r.Items).SingleAsync(r => r.Id == refund.Id, ct));
    }

    private static List<ReturnRefundItem> BuildRefundLines(ReturnRequest request, ReturnRefundCreateDto dto)
    {
        var lines = new List<ReturnRefundItem>();
        foreach (var input in dto.Items)
        {
            var item = request.Items.SingleOrDefault(i => i.OrderItemId == input.OrderItemId);
            var claimed = request.Refunds.Where(r => r.Status != ReturnRefundStatus.Failed)
                .SelectMany(r => r.Items).Where(i => i.OrderItemId == input.OrderItemId)
                .Sum(i => (long)i.Quantity);
            if (item is null || input.Quantity > item.ReceivedQuantity - claimed || item.OrderItem.UnitPrice <= 0)
                throw new ValidationException("La cantidad supera lo recibido disponible para reembolso.");
            lines.Add(new() { OrderItemId = input.OrderItemId, Quantity = input.Quantity, UnitPrice = item.OrderItem.UnitPrice });
        }
        return lines;
    }

    private static decimal ValidateRefundAmount(List<ReturnRefundItem> lines, decimal expectedAmount)
    {
        var amount = lines.Sum(i => i.Quantity * i.UnitPrice);
        if (amount != expectedAmount || amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new ValidationException("El importe no coincide con los precios originales. Actualiza y revisa antes de confirmar.");
        return amount;
    }

    private async Task ValidateRefundBalanceAsync(Order order, decimal amount, CancellationToken ct)
    {
        // Incluye pendientes y revisión: un resultado incierto sigue ocupando el saldo.
        var amounts = await db.ReturnRefunds.Where(r => r.ReturnRequest.OrderId == order.Id && r.Status != ReturnRefundStatus.Failed)
            .Select(r => r.Amount).ToListAsync(ct);
        if (amount > order.Total!.Value - amounts.Sum())
            throw new ValidationException("El reembolso supera el saldo pagado del pedido.");
    }

    // Consulta o crea la operación Stripe asociada a un reembolso pendiente.
    public async Task<ReturnRefundDto?> ReconcileAsync(Guid id, string? recoveryId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var refund = await db.ReturnRefunds.AsNoTracking().Include(r => r.Items).Include(r => r.ReturnRequest).ThenInclude(r => r.Order).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (refund is null) return null;
        if (refund.Status is ReturnRefundStatus.Succeeded or ReturnRefundStatus.Failed || refund.ReturnRequest.Order.PaymentMethod != PaymentMethod.Card) return Response(refund);
        stripe.RequireConfigured();
        if (recoveryId is not null && refund.StripeRefundId is not null && refund.StripeRefundId != recoveryId) throw new ValidationException("El identificador no coincide con el ya registrado.");
        var stripeId = refund.StripeRefundId ?? recoveryId;
        var order = refund.ReturnRequest.Order;
        if (stripeId is null && refund.CreatedAtUtc < DateTime.UtcNow.AddHours(-23))
        {
            await db.ReturnRefunds.Where(r => r.Id == id && r.StripeRefundId == null && r.Status == ReturnRefundStatus.Pending)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, ReturnRefundStatus.NeedsReview), ct);
            refund.Status = ReturnRefundStatus.NeedsReview; return Response(refund);
        }
        var result = await ResolveStripeRefundAsync(refund, order, stripeId, id, ct);
        return await ApplyAsync(id, result, ct);
    }

    private async Task<JsonElement> ResolveStripeRefundAsync(
        ReturnRefund refund,
        Order order,
        string? stripeId,
        Guid refundId,
        CancellationToken ct)
    {
        // Validar siempre el pago original, también al recuperar un ID desde el panel Stripe.
        var session = await stripe.GetAsync(order.StripeSessionId!, ct);
        ValidateOriginalPaymentSession(session, order);
        var intent = session.GetProperty("payment_intent").GetString();
        if (string.IsNullOrWhiteSpace(intent) || (refund.PaymentIntentId != null && refund.PaymentIntentId != intent))
            throw new ValidationException("El pago original cambió.");
        await db.ReturnRefunds.Where(r => r.Id == refundId && r.PaymentIntentId == null)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.PaymentIntentId, intent), ct);
        refund.PaymentIntentId = intent;
        // Revalidar el límite justo antes de POST. Nunca recrear una operación antigua sin ID.
        if (stripeId is null && refund.CreatedAtUtc < DateTime.UtcNow.AddHours(-23))
            throw new ValidationException("El reembolso requiere revisión manual por superar la ventana segura.");
        return stripeId is null
            ? await stripe.RefundReturnAsync(refund, order.Id, ct)
            : await stripe.GetRefundAsync(stripeId, ct);
    }

    private static void ValidateOriginalPaymentSession(JsonElement session, Order order)
    {
        if (session.GetProperty("id").GetString() != order.StripeSessionId ||
            session.GetProperty("status").GetString() != "complete" ||
            session.GetProperty("payment_status").GetString() != "paid" ||
            session.GetProperty("currency").GetString() != "mxn" ||
            session.GetProperty("amount_total").GetInt64() != order.Total * 100 ||
            session.GetProperty("client_reference_id").GetString() != order.Id.ToString() ||
            session.GetProperty("metadata").GetProperty("order_id").GetString() != order.Id.ToString())
            throw new ValidationException("El pago Stripe no coincide con el pedido.");
    }

    // Valida la respuesta de Stripe y actualiza el estado final del reembolso.
    private async Task<ReturnRefundDto> ApplyAsync(Guid id, JsonElement result, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var refund = await db.ReturnRefunds.Include(r => r.Items).Include(r => r.ReturnRequest).ThenInclude(r => r.Order).SingleAsync(r => r.Id == id, ct);
        var stripeId = result.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(stripeId) || result.GetProperty("object").GetString() != "refund" || result.GetProperty("currency").GetString() != "mxn" ||
            result.GetProperty("amount").GetInt64() != refund.Amount * 100 || refund.PaymentIntentId is null || result.GetProperty("payment_intent").GetString() != refund.PaymentIntentId ||
            result.GetProperty("metadata").GetProperty("order_id").GetString() != refund.ReturnRequest.OrderId.ToString() ||
            result.GetProperty("metadata").GetProperty("return_refund_id").GetString() != id.ToString() ||
            (refund.StripeRefundId != null && refund.StripeRefundId != stripeId) || (result.TryGetProperty("livemode", out var live) && live.GetBoolean()))
            throw new ValidationException("La respuesta Stripe no coincide con este reembolso.");
        if (refund.Status is ReturnRefundStatus.Succeeded or ReturnRefundStatus.Failed) return Response(refund);
        refund.StripeRefundId = stripeId;
        refund.Status = result.GetProperty("status").GetString() switch
        {
            "succeeded" => ReturnRefundStatus.Succeeded,
            "failed" or "canceled" => ReturnRefundStatus.Failed,
            "pending" => ReturnRefundStatus.Pending,
            _ => ReturnRefundStatus.NeedsReview
        };
        if (refund.Status is ReturnRefundStatus.Succeeded or ReturnRefundStatus.Failed) refund.CompletedAtUtc = DateTime.UtcNow;
        refund.Version = Guid.NewGuid(); refund.ReturnRequest.Version = Guid.NewGuid(); refund.ReturnRequest.Order.FulfillmentVersion = Guid.NewGuid();
        if (refund.Status == ReturnRefundStatus.Succeeded) AfterSalesEmails.Queue(db, refund.ReturnRequest.Order, "ReturnRefundSucceeded", refund.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Response(refund);
    }
}
