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
    private const string DefaultCurrency = "MXN";
    private const string DefaultShippingName = "Gestionado por la tienda";
    private const int DefaultPageSize = 20;

    private const string DuplicateRequestIdErrorMessage = "El identificador ya corresponde a otro pedido.";
    private const string InvalidItemErrorMessage = "El pedido contiene un artículo inválido.";
    private const string GroupVariantsErrorMessage = "Agrupa las cantidades de la misma variante en un solo artículo.";
    private const string CustomerNotFoundErrorMessage = "La cuenta del cliente ya no está disponible.";
    private const string ProductsUnavailableErrorMessage = "Uno o más productos ya no están disponibles. Actualiza tu carrito.";
    private const string VariantSelectionRequiredErrorMessage = "Selecciona talla y color de cada producto.";
    private const string VariantUnavailableErrorMessage = "La variante seleccionada no está disponible.";
    private const string InsufficientStockErrorMessage = "No hay suficientes unidades de la talla y color seleccionados.";
    private const string ShippingOptionUnavailableErrorMessage = "Selecciona una opción de entrega disponible.";

    // Crea un pedido persistente después de validar productos, variantes y envío.
    public Task<OrderResponseDto> CreateAsync(int customerId, OrderCreateDto dto, CancellationToken ct) =>
        BuildAsync(customerId, dto, persist: true, ct);

    // Calcula la cotización con las reglas actuales, pero sin guardar un pedido.
    public Task<OrderResponseDto> QuoteAsync(int customerId, OrderCreateDto dto, CancellationToken ct) =>
        BuildAsync(customerId, dto, persist: false, ct);

    // Comparte la lógica de cotización y creación para evitar que ambos flujos calculen
    // precios o disponibilidad de forma diferente.
    private async Task<OrderResponseDto> BuildAsync(int customerId, OrderCreateDto dto, bool persist, CancellationToken ct)
    {
        ValidateOrderInput(dto);

        var requestHash = ComputeRequestHash(dto);

        if (persist && dto.ClientRequestId.HasValue)
        {
            var existingOrder = await TryGetExistingOrderAsync(customerId, dto.ClientRequestId.Value, requestHash, ct);
            if (existingOrder is not null)
            {
                return MapToResponse(existingOrder);
            }
        }

        ValidateOrderItemsStructure(dto.Items);

        var productIds = dto.Items.Select(item => item.ProductId).Distinct().ToArray();
        var products = await LoadAndValidateProductsAsync(customerId, productIds, ct);
        var variants = await LoadProductVariantsAsync(productIds, ct);

        await ValidateStockAndVariantsAsync(dto.Items, dto.PaymentMethod, products, variants, ct);

        var shippingOption = await ResolveShippingOptionAsync(dto.ShippingOptionId, ct);

        var order = CreateOrderEntity(customerId, dto, shippingOption, products, variants, requestHash);

        if (persist)
        {
            await SaveOrderWithConcurrencyHandlingAsync(order, customerId, dto.ClientRequestId, requestHash, ct);
        }

        return MapToResponse(order);
    }

    private static void ValidateOrderInput(OrderCreateDto dto)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        Validator.ValidateObject(dto.ShippingAddress!, new ValidationContext(dto.ShippingAddress!), true);
    }

    private static string ComputeRequestHash(OrderCreateDto dto)
    {
        var json = JsonSerializer.Serialize(dto);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }

    private async Task<Order?> TryGetExistingOrderAsync(int customerId, Guid clientRequestId, string requestHash, CancellationToken ct)
    {
        var existingOrder = await db.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.CustomerId == customerId && order.ClientRequestId == clientRequestId, ct);

        if (existingOrder is not null)
        {
            if (existingOrder.RequestHash != requestHash)
            {
                throw new ValidationException(DuplicateRequestIdErrorMessage);
            }
        }

        return existingOrder;
    }

    private static void ValidateOrderItemsStructure(List<OrderItemCreateDto> items)
    {
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new ValidationException(InvalidItemErrorMessage);
            }
            Validator.ValidateObject(item, new ValidationContext(item), true);
        }

        var uniqueVariantsCount = items.Select(item => (item.ProductId, item.VariantId)).Distinct().Count();
        if (uniqueVariantsCount != items.Count)
        {
            throw new ValidationException(GroupVariantsErrorMessage);
        }
    }

    private async Task<Dictionary<int, Product>> LoadAndValidateProductsAsync(int customerId, int[] productIds, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(customer => customer.Id == customerId, ct))
        {
            throw new ValidationException(CustomerNotFoundErrorMessage);
        }

        var products = await db.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id, ct);

        if (products.Count != productIds.Length)
        {
            throw new ValidationException(ProductsUnavailableErrorMessage);
        }

        return products;
    }

    private async Task<Dictionary<int, ProductVariant>> LoadProductVariantsAsync(int[] productIds, CancellationToken ct)
    {
        return await db.ProductVariants
            .AsNoTracking()
            .Where(variant => productIds.Contains(variant.ProductId))
            .ToDictionaryAsync(variant => variant.Id, ct);
    }

    private async Task ValidateStockAndVariantsAsync(
        List<OrderItemCreateDto> items,
        PaymentMethod? paymentMethod,
        Dictionary<int, Product> products,
        Dictionary<int, ProductVariant> variants,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var variantIds = items.Where(item => item.VariantId.HasValue)
            .Select(item => item.VariantId!.Value).Distinct().ToArray();
        var reservedByVariant = await db.InventoryReservations
            .Where(reservation => variantIds.Contains(reservation.VariantId) && !reservation.Released &&
                (reservation.ExpiresAtUtc > now || reservation.Order.StripeHold))
            .GroupBy(reservation => reservation.VariantId)
            .Select(group => new { VariantId = group.Key, Quantity = group.Sum(reservation => reservation.Quantity) })
            .ToDictionaryAsync(item => item.VariantId, item => item.Quantity, ct);

        foreach (var item in items)
        {
            if (item.VariantId is null)
            {
                if (paymentMethod == PaymentMethod.Card || variants.Values.Any(variant => variant.ProductId == item.ProductId))
                {
                    throw new ValidationException(VariantSelectionRequiredErrorMessage);
                }
                continue;
            }

            if (!variants.TryGetValue(item.VariantId.Value, out var variant) || variant.ProductId != item.ProductId || !variant.IsActive)
            {
                throw new ValidationException(VariantUnavailableErrorMessage);
            }

            var reservedQuantity = reservedByVariant.GetValueOrDefault(variant.Id);

            if (variant.Stock - reservedQuantity < item.Quantity)
            {
                throw new ValidationException(InsufficientStockErrorMessage);
            }
        }
    }

    private async Task<ShippingOption?> ResolveShippingOptionAsync(int? shippingOptionId, CancellationToken ct)
    {
        if (!shippingOptionId.HasValue)
        {
            return null;
        }

        var shippingOption = await db.ShippingOptions
            .AsNoTracking()
            .SingleOrDefaultAsync(option => option.Id == shippingOptionId.Value && option.IsActive, ct);

        if (shippingOption is null)
        {
            throw new ValidationException(ShippingOptionUnavailableErrorMessage);
        }

        return shippingOption;
    }

    private static Order CreateOrderEntity(
        int customerId,
        OrderCreateDto dto,
        ShippingOption? shipping,
        Dictionary<int, Product> products,
        Dictionary<int, ProductVariant> variants,
        string requestHash)
    {
        var orderId = Guid.NewGuid();

        var order = new Order
        {
            Id = orderId,
            Number = $"EB-{orderId:N}",
            CreatedAtUtc = DateTime.UtcNow,
            ClientRequestId = dto.ClientRequestId,
            RequestHash = requestHash,
            CustomerId = customerId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Address = dto.ShippingAddress!.FormatAddress(),
            PaymentMethod = dto.PaymentMethod!.Value,
            ShippingOptionId = shipping?.Id,
            ShippingName = shipping?.Name ?? DefaultShippingName,
            ShippingCost = shipping?.Price ?? (dto.PaymentMethod == PaymentMethod.Card ? 0m : null),
            Items = dto.Items.Select(item =>
            {
                var product = products[item.ProductId];
                var variant = item.VariantId.HasValue ? variants[item.VariantId.Value] : null;

                return new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = product.Name,
                    VariantId = item.VariantId,
                    Size = variant?.Size,
                    Color = variant?.Color,
                    ImageUrl = variant?.ImageUrl ?? product.ImageUrl,
                    UnitPrice = product.Price,
                    Quantity = item.Quantity,
                    Details = string.IsNullOrWhiteSpace(item.Details) ? null : item.Details.Trim(),
                    LineTotal = product.Price * item.Quantity
                };
            }).ToList()
        };

        order.Subtotal = order.Items.Sum(item => item.LineTotal);
        order.Total = order.ShippingCost.HasValue ? order.Subtotal + order.ShippingCost.Value : null;

        return order;
    }

    private async Task SaveOrderWithConcurrencyHandlingAsync(
        Order order,
        int customerId,
        Guid? clientRequestId,
        string requestHash,
        CancellationToken ct)
    {
        db.Orders.Add(order);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (clientRequestId.HasValue)
        {
            db.ChangeTracker.Clear();

            var existingOrder = await db.Orders
                .AsNoTracking()
                .Include(ord => ord.Items)
                .SingleOrDefaultAsync(ord => ord.CustomerId == customerId && ord.ClientRequestId == clientRequestId, ct);

            if (existingOrder is null)
            {
                throw;
            }

            if (existingOrder.RequestHash != requestHash)
            {
                throw new ValidationException(DuplicateRequestIdErrorMessage);
            }
        }
    }

    // Obtiene un pedido completo. customerId limita la consulta al propietario;
    // null se utiliza para las consultas administrativas.
    public async Task<OrderResponseDto?> GetAsync(Guid id, int? customerId, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == id && (customerId == null || order.CustomerId == customerId), ct);

        return order is null ? null : MapToResponse(order);
    }

    // Devuelve una página de resúmenes, filtrada por cliente cuando corresponde.
    public Task<List<OrderSummaryDto>> ListAsync(int? customerId, int page, CancellationToken ct) =>
        db.Orders
            .AsNoTracking()
            .Where(order => customerId == null || order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenBy(order => order.Id)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .Select(order => new OrderSummaryDto(
                order.Id,
                order.Number,
                order.CreatedAtUtc,
                order.PaymentMethod,
                order.PaymentStatus,
                order.FulfillmentStatus,
                order.Subtotal,
                order.Total,
                order.Currency,
                order.CancellationPending,
                order.RefundStatus))
            .ToListAsync(ct);

    // Convierte la entidad a la respuesta pública y conserva el orden de los artículos.
    private static OrderResponseDto MapToResponse(Order order) => new(
        order.Id,
        order.Number,
        order.CreatedAtUtc,
        order.CustomerId,
        order.FirstName,
        order.LastName,
        order.Email,
        order.Phone,
        order.Address,
        order.Currency,
        order.PaymentMethod,
        order.PaymentStatus,
        order.FulfillmentStatus,
        order.Subtotal,
        order.ShippingCost,
        order.Total,
        order.Items.OrderBy(item => item.Id).Select(item => new OrderItemResponseDto(
            item.ProductId,
            item.ProductName,
            item.ImageUrl,
            item.UnitPrice,
            item.Quantity,
            item.Details,
            item.LineTotal,
            item.VariantId,
            item.Size,
            item.Color)).ToList(),
        order.ShippingOptionId,
        order.ShippingName,
        order.Carrier,
        order.TrackingNumber,
        order.FulfillmentVersion,
        order.ManualPaymentConfirmedAtUtc,
        order.ManualPaymentMethod,
        order.CancellationPending,
        order.RefundStatus,
        order.RefundedAtUtc,
        order.RestockedAtUtc,
        order.RefundRestock);
}
