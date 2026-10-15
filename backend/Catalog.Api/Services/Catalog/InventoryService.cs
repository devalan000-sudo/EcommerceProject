using System.ComponentModel.DataAnnotations;
using System.Data;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Catalog.Api.Services;

public class InventoryService(CatalogDbContext db)
{
    private const int PaymentReservationMinutes = 15;

    // Devuelve las variantes visibles de un producto y su disponibilidad calculada.
    public Task<List<VariantResponseDto>> ListAsync(int productId, bool admin, CancellationToken ct) =>
        Query(db, productId, admin, DateTime.UtcNow).ToListAsync(ct);

    // Construye la consulta reutilizable de variantes. La disponibilidad es el stock físico
    // menos las reservas que siguen bloqueando unidades.
    public static IQueryable<VariantResponseDto> Query(
        CatalogDbContext db,
        int productId,
        bool admin,
        DateTime now) =>
        db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == productId && (admin || (v.IsActive && v.Product.IsActive)))
            .OrderBy(v => v.Size)
            .ThenBy(v => v.Color)
            .Select(v => new VariantResponseDto(
                v.Id,
                v.Size,
                v.Color,
                v.Stock,
                v.Stock - (v.Reservations
                    .Where(r => !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold))
                    .Sum(r => (int?)r.Quantity) ?? 0),
                v.IsActive,
                v.ImageUrl));

    // Crea o actualiza una combinación de talla y color. La transacción serializable
    // evita inconsistencias si dos operaciones modifican el mismo producto al mismo tiempo.
    public async Task<VariantResponseDto> SaveVariantAsync(
        int productId,
        int? variantId,
        VariantWriteDto dto,
        CancellationToken ct)
    {
        Validator.ValidateObject(dto, new ValidationContext(dto), true);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await EnsureProductExistsAsync(productId, ct);

        var prepared = await PrepareVariantAsync(productId, variantId, dto, ct);
        ApplyVariantChanges(prepared.Variant, prepared.Normalized, dto, prepared.ImageUrl);

        if (!variantId.HasValue)
            db.ProductVariants.Add(prepared.Variant);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ToResponse(prepared.Variant, prepared.HeldQuantity);
    }

    // Agrupa las validaciones y lecturas necesarias antes de modificar la variante.
    private async Task<PreparedVariant> PrepareVariantAsync(
        int productId,
        int? variantId,
        VariantWriteDto dto,
        CancellationToken ct)
    {
        var normalized = NormalizeVariant(dto);
        var existingVariants = await LoadProductVariantsAsync(productId, ct);
        EnsureCombinationIsUnique(existingVariants, normalized, variantId);

        var variant = await FindOrCreateVariantAsync(productId, variantId, ct);
        PreserveExistingSizeCasing(variant, normalized);
        await EnsureVariantHistoryAllowsEditAsync(variant, variantId, normalized, ct);

        var heldQuantity = await GetHeldQuantityAsync(variant.Id, DateTime.UtcNow, ct);
        EnsureReservationCompatibility(variant, variantId, normalized, dto, heldQuantity);

        var imageUrl = await ResolveAndSynchronizeImageAsync(productId, normalized.Color, variantId, dto.ImageUrl, ct);
        return new(variant, normalized, heldQuantity, imageUrl);
    }

    // Reserva las unidades de un pedido durante el inicio del pago con tarjeta.
    // Crear el carrito o el pedido no reserva inventario.
    public async Task<DateTime> ReserveForPaymentAsync(
        Guid orderId,
        int customerId,
        CancellationToken ct,
        bool stripeHold = false,
        string? frontendUrl = null)
    {
        // SERIALIZABLE protege las lecturas de stock y reservas frente a escrituras concurrentes.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var order = await LoadPaymentOrderAsync(orderId, customerId, ct);
        ValidateOrderForCardPayment(order);

        var now = DateTime.UtcNow;
        var existingReservations = await LoadOrderReservationsAsync(orderId, ct);
        if (existingReservations.Count > 0)
            return await ReuseExistingReservationAsync(order!, existingReservations, now, stripeHold, frontendUrl, transaction, ct);

        return await CreatePaymentReservationAsync(order!, now, stripeHold, frontendUrl, transaction, ct);
    }

    // Construye y persiste una reserva nueva dentro de la transacción de pago.
    private async Task<DateTime> CreatePaymentReservationAsync(
        Order order,
        DateTime now,
        bool stripeHold,
        string? frontendUrl,
        IDbContextTransaction transaction,
        CancellationToken ct)
    {
        var expiresAt = now.AddMinutes(PaymentReservationMinutes);
        var reservations = await BuildReservationsAsync(order, expiresAt, now, ct);
        db.InventoryReservations.AddRange(reservations);

        if (stripeHold)
            SetStripeHold(order, expiresAt, frontendUrl);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return expiresAt;
    }

    // Marca como liberadas las reservas de un pedido sin borrar su historial.
    public Task ReleaseAsync(Guid orderId, CancellationToken ct) =>
        db.InventoryReservations
            .Where(r => r.OrderId == orderId && !r.Released)
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.Released, true), ct);

    // Normaliza talla y color para comparar variantes sin duplicados por formato.
    private static NormalizedVariant NormalizeVariant(VariantWriteDto dto) =>
        new(
            VariantTemplateService.NormalizeSize(dto.Size),
            dto.Color.Trim().ToUpperInvariant());

    // Comprueba que el producto padre exista antes de crear una variante.
    private async Task EnsureProductExistsAsync(int productId, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
            throw new ValidationException("Producto no disponible.");
    }

    private Task<List<ProductVariant>> LoadProductVariantsAsync(int productId, CancellationToken ct) =>
        db.ProductVariants.Where(v => v.ProductId == productId).ToListAsync(ct);

    // Impide que un producto tenga dos variantes con la misma talla y color.
    private static void EnsureCombinationIsUnique(
        IEnumerable<ProductVariant> variants,
        NormalizedVariant requested,
        int? variantId)
    {
        var duplicate = variants.Any(v =>
            VariantTemplateService.NormalizeSize(v.Size) == requested.Size &&
            v.Color.Trim().Equals(requested.Color, StringComparison.OrdinalIgnoreCase) &&
            v.Id != variantId);

        if (duplicate)
            throw new ValidationException("Ya existe esa combinación de talla y color.");
    }

    // Obtiene la variante existente o prepara una nueva para el producto.
    private async Task<ProductVariant> FindOrCreateVariantAsync(
        int productId,
        int? variantId,
        CancellationToken ct)
    {
        if (!variantId.HasValue)
            return new ProductVariant { ProductId = productId };

        return await db.ProductVariants.SingleOrDefaultAsync(
                   v => v.Id == variantId && v.ProductId == productId,
                   ct)
               ?? throw new ValidationException("Variante no disponible.");
    }

    // Conserva la forma original de la talla para no cambiar la presentación histórica.
    private static void PreserveExistingSizeCasing(ProductVariant variant, NormalizedVariant requested)
    {
        if (VariantTemplateService.NormalizeSize(variant.Size) == requested.Size)
            requested.Size = variant.Size;
    }

    // Protege la información histórica de pedidos: una variante vendida no puede cambiar
    // su identidad de talla o color.
    private async Task EnsureVariantHistoryAllowsEditAsync(
        ProductVariant variant,
        int? variantId,
        NormalizedVariant requested,
        CancellationToken ct)
    {
        var changesIdentity =
            VariantTemplateService.NormalizeSize(variant.Size) != VariantTemplateService.NormalizeSize(requested.Size) ||
            variant.Color != requested.Color;

        if (variantId.HasValue && changesIdentity && await db.OrderItems.AnyAsync(i => i.VariantId == variant.Id, ct))
        {
            throw new ValidationException(
                "Esta variante tiene pedidos. Conserva su talla y color y crea una combinación nueva para mantener el historial.");
        }
    }

    // Calcula cuántas unidades de una variante están comprometidas por reservas activas.
    private async Task<int> GetHeldQuantityAsync(int variantId, DateTime now, CancellationToken ct) =>
        await db.InventoryReservations
            .Where(r => r.VariantId == variantId && !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold))
            .SumAsync(r => (int?)r.Quantity, ct) ?? 0;

    // Evita reducir o desactivar una variante cuando todavía tiene unidades reservadas.
    private static void EnsureReservationCompatibility(
        ProductVariant variant,
        int? variantId,
        NormalizedVariant requested,
        VariantWriteDto dto,
        int heldQuantity)
    {
        var identityChanged = variantId.HasValue &&
            (VariantTemplateService.NormalizeSize(variant.Size) != VariantTemplateService.NormalizeSize(requested.Size) ||
             variant.Color != requested.Color);

        if (dto.Stock < heldQuantity || (heldQuantity > 0 && (!dto.IsActive || identityChanged)))
        {
            throw new ValidationException(
                "Hay unidades reservadas. No puedes reducirlas ni cambiar o desactivar esa combinación hasta que termine la reserva.");
        }
    }

    // Resuelve la imagen de la variante y la sincroniza con las tallas del mismo color.
    // Resuelve la imagen y sincroniza las variantes existentes del mismo color.
    private async Task<string?> ResolveAndSynchronizeImageAsync(
        int productId,
        string color,
        int? variantId,
        string? requestedImageUrl,
        CancellationToken ct)
    {
        var imageUrl = string.IsNullOrWhiteSpace(requestedImageUrl) ? null : requestedImageUrl.Trim();
        var sameColor = await db.ProductVariants
            .Where(v => v.ProductId == productId && v.Color == color)
            .ToListAsync(ct);

        if (!variantId.HasValue && imageUrl is null)
            imageUrl = sameColor.FirstOrDefault(v => v.ImageUrl != null)?.ImageUrl;

        foreach (var sibling in sameColor)
            sibling.ImageUrl = imageUrl;

        return imageUrl;
    }

    // Aplica al modelo los valores ya validados de la variante.
    private static void ApplyVariantChanges(
        ProductVariant variant,
        NormalizedVariant normalized,
        VariantWriteDto dto,
        string? imageUrl)
    {
        variant.Size = normalized.Size;
        variant.Color = normalized.Color;
        variant.Stock = dto.Stock;
        variant.IsActive = dto.IsActive;
        variant.ImageUrl = imageUrl;
    }

    // Convierte la entidad en la respuesta pública con disponibilidad calculada.
    private static VariantResponseDto ToResponse(ProductVariant variant, int heldQuantity) =>
        new(variant.Id, variant.Size, variant.Color, variant.Stock, variant.Stock - heldQuantity, variant.IsActive, variant.ImageUrl);

    // Carga el pedido y sus artículos, restringiéndolo al cliente autenticado.
    private Task<Order?> LoadPaymentOrderAsync(Guid orderId, int customerId, CancellationToken ct) =>
        db.Orders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);

    // Verifica que el pedido esté en el estado correcto para iniciar el pago.
    private static void ValidateOrderForCardPayment(Order? order)
    {
        if (order is null || order.CancellationPending || order.PaymentMethod != PaymentMethod.Card ||
            order.PaymentStatus != PaymentStatus.Pending ||
            order.FulfillmentStatus != FulfillmentStatus.PendingConfirmation ||
            order.Total is null || order.Items.Count == 0)
        {
            throw new ValidationException("El pedido no está listo para pagar con tarjeta.");
        }
    }

    private Task<List<InventoryReservation>> LoadOrderReservationsAsync(Guid orderId, CancellationToken ct) =>
        db.InventoryReservations.Where(r => r.OrderId == orderId).ToListAsync(ct);

    // Reutiliza una reserva existente en reintentos de pago sin prolongar su vencimiento.
    private async Task<DateTime> ReuseExistingReservationAsync(
        Order order,
        IReadOnlyCollection<InventoryReservation> reservations,
        DateTime now,
        bool stripeHold,
        string? frontendUrl,
        IDbContextTransaction transaction,
        CancellationToken ct)
    {
        if (reservations.Any(r => r.Released || r.ExpiresAtUtc <= now))
            throw new ValidationException("La reserva venció. Crea un pedido nuevo para volver a comprobar disponibilidad.");

        if (stripeHold)
        {
            order.StripeHold = true;
            order.PaymentDeadlineUtc = reservations.Min(r => r.ExpiresAtUtc);
            order.StripeFrontendUrl = frontendUrl;
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return reservations.Min(r => r.ExpiresAtUtc);
    }

    // Comprueba disponibilidad de cada artículo y construye las reservas del pedido.
    private async Task<List<InventoryReservation>> BuildReservationsAsync(
        Order order,
        DateTime expiresAt,
        DateTime now,
        CancellationToken ct)
    {
        var variantIds = order.Items.Where(item => item.VariantId.HasValue)
            .Select(item => item.VariantId!.Value).Distinct().ToArray();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id) && v.IsActive && v.Product.IsActive)
            .ToDictionaryAsync(v => v.Id, ct);
        var heldByVariant = await db.InventoryReservations
            .Where(r => variantIds.Contains(r.VariantId) && !r.Released &&
                (r.ExpiresAtUtc > now || r.Order.StripeHold))
            .GroupBy(r => r.VariantId)
            .Select(group => new { VariantId = group.Key, Quantity = group.Sum(r => r.Quantity) })
            .ToDictionaryAsync(x => x.VariantId, x => x.Quantity, ct);

        var reservations = new List<InventoryReservation>();
        foreach (var item in order.Items.OrderBy(i => i.VariantId))
        {
            if (!item.VariantId.HasValue || !variants.TryGetValue(item.VariantId.Value, out var variant) || variant.ProductId != item.ProductId)
                throw new ValidationException("Una variante ya no está disponible.");

            var heldQuantity = heldByVariant.GetValueOrDefault(variant.Id);
            if (variant.Stock - heldQuantity < item.Quantity)
                throw new ValidationException("Ya no hay suficientes unidades para este pedido.");

            reservations.Add(new InventoryReservation
            {
                OrderId = order.Id,
                VariantId = variant.Id,
                Quantity = item.Quantity,
                ExpiresAtUtc = expiresAt
            });
        }

        return reservations;
    }

    // Marca el pedido como retenido mientras Stripe termina el proceso de pago.
    private static void SetStripeHold(Order order, DateTime expiresAt, string? frontendUrl)
    {
        order.StripeHold = true;
        order.PaymentDeadlineUtc = expiresAt;
        order.StripeFrontendUrl = frontendUrl;
    }

    private sealed class NormalizedVariant(string size, string color)
    {
        public string Size { get; set; } = size;
        public string Color { get; } = color;
    }

    private sealed record PreparedVariant(
        ProductVariant Variant,
        NormalizedVariant Normalized,
        int HeldQuantity,
        string? ImageUrl);
}
