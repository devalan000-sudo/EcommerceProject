using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class OrderService(CatalogDbContext db)
{
    public Task<OrderResponseDto> CreateAsync(int customerId, OrderCreateDto dto, CancellationToken ct) => BuildAsync(customerId, dto, true, ct);
    public Task<OrderResponseDto> QuoteAsync(int customerId, OrderCreateDto dto, CancellationToken ct) => BuildAsync(customerId, dto, false, ct);

    private async Task<OrderResponseDto> BuildAsync(int customerId, OrderCreateDto dto, bool persist, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        Validator.ValidateObject(dto.ShippingAddress!, new ValidationContext(dto.ShippingAddress!), true);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))));
        if (persist && dto.ClientRequestId.HasValue)
        {
            var existing = await db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.CustomerId == customerId && o.ClientRequestId == dto.ClientRequestId, ct);
            if (existing is not null)
            {
                if (existing.RequestHash != hash) throw new ValidationException("El identificador ya corresponde a otro pedido.");
                return Response(existing);
            }
        }
        foreach (var item in dto.Items)
        {
            if (item is null) throw new ValidationException("El pedido contiene un artículo inválido.");
            Validator.ValidateObject(item, new ValidationContext(item), true);
        }
        var ids = dto.Items.Select(i => i.ProductId).Distinct().ToArray();
        if (dto.Items.Select(i => (i.ProductId, i.VariantId)).Distinct().Count() != dto.Items.Count)
            throw new ValidationException("Agrupa las cantidades de la misma variante en un solo artículo.");
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            throw new ValidationException("La cuenta del cliente ya no está disponible.");
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive).ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != ids.Length)
            throw new ValidationException("Uno o más productos ya no están disponibles. Actualiza tu carrito.");

        var variants = await db.ProductVariants.AsNoTracking().Where(v => ids.Contains(v.ProductId)).ToDictionaryAsync(v => v.Id, ct);
        foreach (var item in dto.Items)
        {
            if (item.VariantId is null)
            {
                if (dto.PaymentMethod == PaymentMethod.Card || variants.Values.Any(v => v.ProductId == item.ProductId))
                    throw new ValidationException("Selecciona talla y color de cada producto.");
                continue; // Catálogo anterior: consulta por WhatsApp sin disponibilidad confirmada.
            }
            if (!variants.TryGetValue(item.VariantId.Value, out var variant) || variant.ProductId != item.ProductId || !variant.IsActive)
                throw new ValidationException("La variante seleccionada no está disponible.");
            var now = DateTime.UtcNow;
            var reserved = await db.InventoryReservations.Where(r => r.VariantId == variant.Id && !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold)).SumAsync(r => (int?)r.Quantity, ct) ?? 0;
            if (variant.Stock - reserved < item.Quantity) throw new ValidationException("No hay suficientes unidades de la talla y color seleccionados.");
        }
        var shipping = dto.ShippingOptionId.HasValue
            ? await db.ShippingOptions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == dto.ShippingOptionId && s.IsActive, ct) : null;
        if (dto.ShippingOptionId.HasValue && shipping is null)
            throw new ValidationException("Selecciona una opción de entrega disponible.");

        var id = Guid.NewGuid();
        var order = new Order
        {
            Id = id,
            Number = $"EB-{id:N}",
            CreatedAtUtc = DateTime.UtcNow,
            ClientRequestId = dto.ClientRequestId,
            RequestHash = hash,
            CustomerId = customerId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Address = dto.ShippingAddress!.FormatAddress(),
            PaymentMethod = dto.PaymentMethod!.Value,
            ShippingOptionId = shipping?.Id,
            ShippingName = shipping?.Name ?? "Gestionado por la tienda",
            ShippingCost = shipping?.Price ?? (dto.PaymentMethod == PaymentMethod.Card ? 0m : null),
            Items = dto.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = products[i.ProductId].Name,
                VariantId = i.VariantId,
                Size = i.VariantId.HasValue ? variants[i.VariantId.Value].Size : null,
                Color = i.VariantId.HasValue ? variants[i.VariantId.Value].Color : null,
                ImageUrl = i.VariantId.HasValue ? variants[i.VariantId.Value].ImageUrl ?? products[i.ProductId].ImageUrl : products[i.ProductId].ImageUrl,
                UnitPrice = products[i.ProductId].Price,
                Quantity = i.Quantity,
                Details = string.IsNullOrWhiteSpace(i.Details) ? null : i.Details.Trim(),
                LineTotal = products[i.ProductId].Price * i.Quantity
            }).ToList()
        };
        order.Subtotal = order.Items.Sum(i => i.LineTotal);
        order.Total = order.ShippingCost.HasValue ? order.Subtotal + order.ShippingCost.Value : null;
        if (persist)
        {
            db.Orders.Add(order);
            // Una sola operación SaveChanges guarda cabecera e ítems en una transacción.
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) when (dto.ClientRequestId.HasValue)
            {
                db.ChangeTracker.Clear();
                var existing = await db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.CustomerId == customerId && o.ClientRequestId == dto.ClientRequestId, ct);
                if (existing is null) throw;
                if (existing.RequestHash != hash) throw new ValidationException("El identificador ya corresponde a otro pedido.");
                return Response(existing);
            }
        }
        return Response(order);
    }

    public async Task<OrderResponseDto?> GetAsync(Guid id, int? customerId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == id && (customerId == null || o.CustomerId == customerId), ct);
        return order is null ? null : Response(order);
    }

    public Task<List<OrderSummaryDto>> ListAsync(int? customerId, int page, CancellationToken ct) =>
        db.Orders.AsNoTracking().Where(o => customerId == null || o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAtUtc).ThenBy(o => o.Id)
            .Skip((page - 1) * 20).Take(20)
            .Select(o => new OrderSummaryDto(o.Id, o.Number, o.CreatedAtUtc, o.PaymentMethod,
                o.PaymentStatus, o.FulfillmentStatus, o.Subtotal, o.Total, o.Currency,
                o.CancellationPending, o.RefundStatus)).ToListAsync(ct);

    private static OrderResponseDto Response(Order o) => new(o.Id, o.Number, o.CreatedAtUtc,
        o.CustomerId, o.FirstName, o.LastName, o.Email, o.Phone, o.Address, o.Currency,
        o.PaymentMethod, o.PaymentStatus, o.FulfillmentStatus, o.Subtotal, o.ShippingCost, o.Total,
        o.Items.OrderBy(i => i.Id).Select(i => new OrderItemResponseDto(i.ProductId, i.ProductName,
            i.ImageUrl, i.UnitPrice, i.Quantity, i.Details, i.LineTotal, i.VariantId, i.Size, i.Color)).ToList(),
        o.ShippingOptionId, o.ShippingName, o.Carrier, o.TrackingNumber, o.FulfillmentVersion,
        o.ManualPaymentConfirmedAtUtc, o.ManualPaymentMethod,
        o.CancellationPending, o.RefundStatus, o.RefundedAtUtc, o.RestockedAtUtc, o.RefundRestock);
}
