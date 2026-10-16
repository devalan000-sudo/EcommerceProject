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

public class ReturnService(CatalogDbContext db)
{
    // Explica por qué un pedido no puede iniciar una devolución.
    private static string? Unavailable(Order order) =>
        order.PaymentStatus != PaymentStatus.Paid || order.StripeHold || order.CancellationPending || order.RefundRequestId.HasValue || order.RefundStatus != null
            ? "Solo puedes solicitar devoluciones de pedidos pagados sin cancelación o reembolso en curso."
            : order.FulfillmentStatus is not (FulfillmentStatus.Shipped or FulfillmentStatus.Delivered)
                ? "Las devoluciones se solicitan después del envío o la entrega. Para cancelar antes, contacta al negocio." : null;

    // Suma las cantidades que ya están comprometidas en solicitudes no rechazadas.
    private async Task<Dictionary<int, int>> Claimed(Guid orderId, CancellationToken ct) =>
        await db.ReturnRequestItems.Where(i => i.ReturnRequest.OrderId == orderId && i.ReturnRequest.Status != ReturnStatus.Rejected)
            .GroupBy(i => i.OrderItemId).Select(g => new { Id = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(g => g.Id, g => g.Quantity, ct);

    // Devuelve qué artículos y cantidades todavía pueden solicitarse para devolución.
    public async Task<ReturnOptionsDto?> OptionsAsync(Guid orderId, int customerId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
        if (order is null) return null;
        var claimed = await Claimed(orderId, ct);
        var items = order.Items.OrderBy(i => i.Id).Select(i => Item(i, Math.Max(0, i.Quantity - claimed.GetValueOrDefault(i.Id)))).ToList();
        var reason = Unavailable(order) ?? (items.All(i => i.Quantity == 0) ? "Todos los artículos ya tienen una solicitud pendiente o aprobada." : null);
        return new(reason is null, reason, items);
    }

    // Lista devoluciones del cliente o del administrador con filtros y paginación.
    public async Task<ReturnPageDto> ListAsync(int? customerId, Guid? orderId, ReturnStatus? status, int page, CancellationToken ct)
    {
        if (page < 1 || page > 100000 || (status.HasValue && !Enum.IsDefined(status.Value))) throw new ValidationException("Filtro no válido.");
        var query = db.ReturnRequests.AsNoTracking().AsQueryable();
        if (customerId.HasValue) query = query.Where(r => r.Order.CustomerId == customerId);
        if (orderId.HasValue) query = query.Where(r => r.OrderId == orderId);
        if (status.HasValue) query = query.Where(r => r.Status == status);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
            .Skip((page - 1) * 20).Take(20).Include(r => r.Order).Include(r => r.Items).ThenInclude(i => i.OrderItem).Include(r => r.Receipts).ThenInclude(r => r.Items).Include(r => r.Refunds).ThenInclude(r => r.Items).ToListAsync(ct);
        return new(rows.Select(Response).ToList(), page, 20, total);
    }

    // Registra una solicitud de devolución idempotente y valida sus cantidades.
    public async Task<ReturnResponseDto?> CreateAsync(Guid orderId, int customerId, ReturnCreateDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        if (dto.RequestId == Guid.Empty || dto.Items.Any(i => i is null || i.OrderItemId <= 0 || i.Quantity <= 0) ||
            dto.Items.Select(i => i.OrderItemId).Distinct().Count() != dto.Items.Count)
            throw new ValidationException("Selecciona artículos distintos, cantidades positivas y un identificador de solicitud válido.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Reason = dto.Reason.Trim(),
            Items = dto.Items.OrderBy(i => i.OrderItemId).Select(i => new { i.OrderItemId, i.Quantity })
        }))));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);
        if (order is null) return null;
        var existing = await db.ReturnRequests.Include(r => r.Items).ThenInclude(i => i.OrderItem).Include(r => r.Receipts).ThenInclude(r => r.Items).Include(r => r.Refunds).ThenInclude(r => r.Items)
            .SingleOrDefaultAsync(r => r.OrderId == orderId && r.ClientRequestId == dto.RequestId, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new ValidationException("Este identificador ya se utilizó con otros datos. Actualiza las solicitudes.");
            return Response(existing);
        }
        if (Unavailable(order) is { } reason) throw new ValidationException(reason);
        var claimed = await Claimed(orderId, ct);
        foreach (var requested in dto.Items)
        {
            var item = order.Items.SingleOrDefault(i => i.Id == requested.OrderItemId);
            if (item is null || requested.Quantity > item.Quantity - claimed.GetValueOrDefault(item.Id))
                throw new ValidationException("Una cantidad supera lo disponible para devolver o el artículo no pertenece al pedido. Actualiza e inténtalo de nuevo.");
        }
        var request = new ReturnRequest
        {
            Order = order,
            OrderId = orderId,
            ClientRequestId = dto.RequestId,
            RequestHash = hash,
            Reason = dto.Reason.Trim(),
            Items = dto.Items.Select(i => new ReturnRequestItem { OrderItemId = i.OrderItemId, Quantity = i.Quantity }).ToList()
        };
        db.ReturnRequests.Add(request);
        // Serializa solicitudes del mismo pedido y cambios administrativos concurrentes.
        order.FulfillmentVersion = Guid.NewGuid();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Response(request);
    }

    // Aprueba o rechaza una solicitud antes de que se reciban los artículos.
    public async Task<ReturnResponseDto?> ReviewAsync(Guid id, int adminId, ReturnReviewDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        if (dto.Status is not (ReturnStatus.Approved or ReturnStatus.Rejected)) throw new ValidationException("Solo puedes aprobar o rechazar una solicitud pendiente.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.ReturnRequests.Include(r => r.Order).Include(r => r.Items).ThenInclude(i => i.OrderItem).Include(r => r.Receipts).ThenInclude(r => r.Items).Include(r => r.Refunds).ThenInclude(r => r.Items).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return null;
        if (request.Version != dto.Version) throw new DbUpdateConcurrencyException();
        if (request.Status != ReturnStatus.Requested) throw new ValidationException("La solicitud ya fue revisada.");
        if (dto.Status == ReturnStatus.Approved && Unavailable(request.Order) is { } reason) throw new ValidationException(reason);
        request.Status = dto.Status; request.DecisionNote = dto.DecisionNote.Trim();
        request.ReviewedAtUtc = DateTime.UtcNow; request.ReviewedByAdminId = adminId; request.Version = Guid.NewGuid();
        request.Order.FulfillmentVersion = Guid.NewGuid();
        AfterSalesEmails.Queue(db, request.Order, dto.Status == ReturnStatus.Approved ? "ReturnApproved" : "ReturnRejected", request.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Response(request);
    }

    // Registra artículos recibidos y repone únicamente las unidades autorizadas.
    public async Task<ReturnResponseDto?> ReceiveAsync(Guid id, int adminId, ReturnReceiveDto dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        ValidateReceiveInput(dto);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Note = dto.Note.Trim(),
            Items = dto.Items.OrderBy(i => i.OrderItemId).Select(i => new { i.OrderItemId, i.ReceivedQuantity, i.RestockedQuantity })
        }))));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.ReturnRequests.Include(r => r.Order).Include(r => r.Items).ThenInclude(i => i.OrderItem)
            .Include(r => r.Receipts).ThenInclude(r => r.Items).Include(r => r.Refunds).ThenInclude(r => r.Items).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return null;
        var previous = request.Receipts.SingleOrDefault(r => r.ClientRequestId == dto.RequestId);
        if (previous is not null)
        {
            if (previous.RequestHash != hash) throw new ValidationException("Esta recepción ya se registró con otros datos. Actualiza el historial.");
            return Response(request);
        }
        if (request.Version != dto.Version) throw new DbUpdateConcurrencyException();
        if (request.Status is not (ReturnStatus.Approved or ReturnStatus.PartiallyReceived))
            throw new ValidationException("Solo se reciben devoluciones aprobadas con unidades pendientes.");
        if (Unavailable(request.Order) is { } reason) throw new ValidationException(reason);

        var lines = BuildReceiveLines(request, dto);
        var adjustments = await BuildRestockAdjustmentsAsync(lines, ct);
        // Validar todo antes de modificar; recepción e inventario se confirman juntos.
        foreach (var (variant, quantity) in adjustments) variant.Stock += quantity;
        foreach (var (item, input) in lines) { item.ReceivedQuantity += input.ReceivedQuantity; item.RestockedQuantity += input.RestockedQuantity; }
        var receipt = new ReturnReceipt
        {
            ReturnRequest = request,
            ReturnRequestId = request.Id,
            ClientRequestId = dto.RequestId,
            RequestHash = hash,
            AdminId = adminId,
            Note = dto.Note.Trim(),
            Items = dto.Items.Select(i => new ReturnReceiptItem { OrderItemId = i.OrderItemId, ReceivedQuantity = i.ReceivedQuantity, RestockedQuantity = i.RestockedQuantity }).ToList()
        };
        db.ReturnReceipts.Add(receipt);
        AfterSalesEmails.Queue(db, request.Order, "ReturnReceived", receipt.Id);
        request.Status = request.Items.All(i => i.ReceivedQuantity == i.Quantity) ? ReturnStatus.Received : ReturnStatus.PartiallyReceived;
        request.Version = Guid.NewGuid(); request.Order.FulfillmentVersion = Guid.NewGuid();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Response(request);
    }

    private static List<(ReturnRequestItem Item, ReturnReceiveItemDto Input)> BuildReceiveLines(
        ReturnRequest request,
        ReturnReceiveDto dto)
    {
        var lines = new List<(ReturnRequestItem Item, ReturnReceiveItemDto Input)>();
        foreach (var input in dto.Items)
        {
            var item = request.Items.SingleOrDefault(i => i.OrderItemId == input.OrderItemId);
            if (item is null || input.ReceivedQuantity > item.Quantity - item.ReceivedQuantity)
                throw new ValidationException("Un artículo no pertenece a la devolución o supera la cantidad pendiente de recibir.");
            lines.Add((item, input));
        }
        return lines;
    }

    private async Task<List<(ProductVariant Variant, int Quantity)>> BuildRestockAdjustmentsAsync(
        List<(ReturnRequestItem Item, ReturnReceiveItemDto Input)> lines,
        CancellationToken ct)
    {
        var variantIds = lines.Where(l => l.Input.RestockedQuantity > 0 && l.Item.OrderItem.VariantId.HasValue)
            .Select(l => l.Item.OrderItem.VariantId!.Value).Distinct().ToArray();
        var variants = await db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var adjustments = new List<(ProductVariant Variant, int Quantity)>();
        foreach (var group in lines.Where(l => l.Input.RestockedQuantity > 0)
            .GroupBy(l => l.Item.OrderItem.VariantId).OrderBy(g => g.Key))
        {
            var variant = group.Key.HasValue && variants.TryGetValue(group.Key.Value, out var loadedVariant) ? loadedVariant : null;
            var quantity = group.Sum(l => (long)l.Input.RestockedQuantity);
            if (variant is null || group.Any(l => l.Item.OrderItem.ProductId != variant.ProductId ||
                    l.Item.OrderItem.Size != variant.Size || l.Item.OrderItem.Color != variant.Color) ||
                variant.Stock < 0 || quantity > 1000000L - variant.Stock)
                throw new ValidationException("No se puede reponer una variante eliminada, modificada o sin capacidad. Revisa el inventario o registra la recepción sin reposición.");
            adjustments.Add((variant, (int)quantity));
        }
        return adjustments;
    }

    private static void ValidateReceiveInput(ReturnReceiveDto dto)
    {
        if (dto.RequestId == Guid.Empty || dto.Items.Any(i => i is null || i.OrderItemId <= 0 ||
                i.ReceivedQuantity <= 0 || i.RestockedQuantity < 0 || i.RestockedQuantity > i.ReceivedQuantity) ||
            dto.Items.Select(i => i.OrderItemId).Distinct().Count() != dto.Items.Count)
            throw new ValidationException("Revisa las cantidades: recibidas positivas, reposición entre cero y lo recibido, sin artículos repetidos.");
    }

    // Convierte una línea del pedido en una línea de devolución.
    private static ReturnItemDto Item(OrderItem i, int quantity, int received = 0, int restocked = 0) => new(i.Id, i.ProductName, i.Size, i.Color, i.ImageUrl, quantity, received, restocked, i.UnitPrice);
    // Convierte la solicitud completa con sus recepciones y reembolsos.
    private static ReturnResponseDto Response(ReturnRequest r) => new(r.Id, r.OrderId, r.Order.Number, r.Status, r.Reason,
        r.CreatedAtUtc, r.ReviewedAtUtc, r.DecisionNote, r.Version, r.Items.OrderBy(i => i.OrderItemId).Select(i => Item(i.OrderItem, i.Quantity, i.ReceivedQuantity, i.RestockedQuantity)).ToList(),
        r.Receipts.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Select(x => new ReturnReceiptDto(x.Id, x.CreatedAtUtc, x.Note,
            x.Items.OrderBy(i => i.OrderItemId).Select(i => new ReturnReceiptItemDto(i.OrderItemId, i.ReceivedQuantity, i.RestockedQuantity)).ToList())).ToList(), r.Order.PaymentMethod, r.Refunds.OrderBy(x => x.CreatedAtUtc).Select(ReturnRefundService.Response).ToList());
}
