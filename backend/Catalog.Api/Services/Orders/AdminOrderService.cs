using System.ComponentModel.DataAnnotations;
using System.Data;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class AdminOrderService(CatalogDbContext db, OrderService orders)
{
    // Busca y pagina pedidos para la administración con filtros de negocio.
    public async Task<AdminOrderPage> ListAsync(AdminOrderQuery filter, CancellationToken ct)
    {
        Validator.ValidateObject(filter, new ValidationContext(filter), true);
        var query = db.Orders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(o => o.Number.Contains(search) || o.Email.Contains(search) || (o.FirstName + " " + o.LastName).Contains(search));
        }
        if (filter.PaymentMethod.HasValue) query = query.Where(o => o.PaymentMethod == filter.PaymentMethod);
        if (filter.PaymentStatus.HasValue) query = query.Where(o => o.PaymentStatus == filter.PaymentStatus);
        if (filter.FulfillmentStatus.HasValue) query = query.Where(o => o.FulfillmentStatus == filter.FulfillmentStatus);
        if (filter.From.HasValue) { var start = filter.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc); query = query.Where(o => o.CreatedAtUtc >= start); }
        if (filter.To.HasValue) { var end = filter.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc); query = query.Where(o => o.CreatedAtUtc < end); }
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAtUtc).ThenBy(o => o.Id).Skip((filter.Page - 1) * 20).Take(20)
            .Select(o => new AdminOrderRow(o.Id, o.Number, o.CreatedAtUtc, o.FirstName, o.LastName, o.Email, o.PaymentMethod, o.PaymentStatus, o.FulfillmentStatus, o.Total, o.Currency)).ToListAsync(ct);
        return new(items, filter.Page, 20, count);
    }

    // Avanza el estado logístico respetando la secuencia permitida y el control de concurrencia.
    public async Task<OrderResponseDto?> UpdateAsync(Guid id, FulfillmentUpdateDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return null;
        if (order.FulfillmentVersion != dto.Version) throw new DbUpdateConcurrencyException();
        ValidateFulfillmentUpdate(order, dto);
        ApplyFulfillmentUpdate(order, dto);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await orders.GetAsync(id, null, ct);
    }

    private static void ValidateFulfillmentUpdate(Order order, FulfillmentUpdateDto dto)
    {
        if (order.CancellationPending || order.RefundRequestId.HasValue)
            throw new ValidationException("El pedido tiene una cancelación o reembolso en curso. Revisa su resultado antes de cambiar la entrega.");
        if (order.FulfillmentStatus is FulfillmentStatus.Cancelled or FulfillmentStatus.Delivered)
            throw new ValidationException("Este pedido ya está cerrado.");
        if (order.PaymentMethod == PaymentMethod.Card && order.PaymentStatus != PaymentStatus.Paid)
            throw new ValidationException("Stripe debe confirmar el pago antes de preparar o enviar el pedido.");

        var next = dto.Status!.Value;
        var valid = next == order.FulfillmentStatus || (order.FulfillmentStatus, next) is
            (FulfillmentStatus.PendingConfirmation, FulfillmentStatus.Preparing) or
            (FulfillmentStatus.Preparing, FulfillmentStatus.Shipped) or
            (FulfillmentStatus.Preparing, FulfillmentStatus.Delivered) or
            (FulfillmentStatus.Shipped, FulfillmentStatus.Delivered);
        if (!valid)
            throw new ValidationException("El cambio de estado no está permitido. Avanza desde el estado actual.");

        var carrier = string.IsNullOrWhiteSpace(dto.Carrier) ? null : dto.Carrier.Trim();
        var tracking = string.IsNullOrWhiteSpace(dto.TrackingNumber) ? null : dto.TrackingNumber.Trim();
        if ((carrier is null) != (tracking is null))
            throw new ValidationException("Captura juntos la paquetería y el número de guía, o deja ambos vacíos para entrega local o recogida.");
    }

    private static void ApplyFulfillmentUpdate(Order order, FulfillmentUpdateDto dto)
    {
        order.FulfillmentStatus = dto.Status!.Value;
        order.Carrier = string.IsNullOrWhiteSpace(dto.Carrier) ? null : dto.Carrier.Trim();
        order.TrackingNumber = string.IsNullOrWhiteSpace(dto.TrackingNumber) ? null : dto.TrackingNumber.Trim();
        order.FulfillmentVersion = Guid.NewGuid();
    }
}
