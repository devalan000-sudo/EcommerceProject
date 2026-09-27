using System.ComponentModel.DataAnnotations;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class DashboardService(CatalogDbContext db)
{
    public async Task<DashboardResponse> GetAsync(DashboardQuery query, CancellationToken ct)
    {
        Validator.ValidateObject(query, new ValidationContext(query), true);
        var now = DateTime.UtcNow;
        var to = query.To ?? DateOnly.FromDateTime(now);
        var from = query.From ?? to.AddDays(-Math.Min(29, to.DayNumber));
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var all = db.Orders.AsNoTracking();
        var period = all.Where(o => o.CreatedAtUtc >= start && o.CreatedAtUtc < end);
        var created = await period.CountAsync(ct);
        int paid = 0, refunded = 0, excluded = 0, cardTests = 0;
        decimal paidTotal = 0, refundedTotal = 0;


        // Proyección mínima y lectura secuencial; no materializa entidades ni pierde precisión decimal.
        await foreach (var o in period.Where(o => o.PaymentStatus == PaymentStatus.Paid || o.PaymentStatus == PaymentStatus.Refunded)
            .Select(o => new { o.PaymentStatus, o.Total, o.Currency, o.PaymentMethod }).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (o.Currency != "MXN" || !o.Total.HasValue) { excluded++; continue; }
            if (o.PaymentStatus == PaymentStatus.Paid) { paid++; paidTotal += o.Total.Value; }
            else { refunded++; refundedTotal += o.Total.Value; }
            if (o.PaymentMethod == PaymentMethod.Card) cardTests++;
        }
        var partials = await db.ReturnRefunds.AsNoTracking().Where(r => r.Status == ReturnRefundStatus.Succeeded &&
            r.ReturnRequest.Order.CreatedAtUtc >= start && r.ReturnRequest.Order.CreatedAtUtc < end &&
            r.ReturnRequest.Order.Currency == "MXN" && r.ReturnRequest.Order.Total != null)
            .Select(r => new { r.Amount, r.ReturnRequest.OrderId }).ToListAsync(ct);
        var partialTotal = partials.Sum(r => r.Amount);
        paidTotal -= partialTotal; refundedTotal += partialTotal;
        refunded += partials.Select(r => r.OrderId).Distinct().Count();
        var open = all.Where(o => o.FulfillmentStatus != FulfillmentStatus.Cancelled && o.FulfillmentStatus != FulfillmentStatus.Delivered);
        var pending = await all.CountAsync(o => o.PaymentStatus == PaymentStatus.Pending && o.FulfillmentStatus != FulfillmentStatus.Cancelled, ct);
        var actionable = open.Where(o => !o.CancellationPending && o.RefundStatus == null);
        var waiting = await actionable.CountAsync(o => o.PaymentStatus == PaymentStatus.Paid && o.FulfillmentStatus == FulfillmentStatus.PendingConfirmation, ct);
        var preparing = await actionable.CountAsync(o => o.FulfillmentStatus == FulfillmentStatus.Preparing, ct);
        var shipped = await actionable.CountAsync(o => o.FulfillmentStatus == FulfillmentStatus.Shipped, ct);
        var resolutions = await all.CountAsync(o => o.CancellationPending ||
            (o.RefundStatus != null && o.RefundStatus != "Succeeded") ||
            (o.RefundStatus == "Succeeded" && o.RefundRestock && o.RestockedAtUtc == null) ||
            db.ReturnRefunds.Any(r => r.ReturnRequest.OrderId == o.Id && (r.Status == ReturnRefundStatus.Pending || r.Status == ReturnRefundStatus.NeedsReview)), ct);
        var available = db.ProductVariants.AsNoTracking().Where(v => v.IsActive && v.Product.IsActive)
            .Select(v => new
            {
                v.ProductId,
                ProductName = v.Product.Name,
                VariantId = v.Id,
                v.Size,
                v.Color,
                v.Stock,
                AvailableStock = v.Stock - (v.Reservations.Where(r => !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold)).Sum(r => (int?)r.Quantity) ?? 0)
            });
        var low = available.Where(v => v.AvailableStock <= query.StockThreshold);
        var lowCount = await low.CountAsync(ct);
        var outCount = await available.CountAsync(v => v.AvailableStock <= 0, ct);
        var missingVariants = await db.Products.AsNoTracking().CountAsync(p => p.IsActive && !p.Variants.Any(v => v.IsActive), ct);
        const int pageSize = 20;
        var rows = await low.OrderBy(v => v.AvailableStock).ThenBy(v => v.ProductId).ThenBy(v => v.VariantId)
            .Skip((query.StockPage - 1) * pageSize).Take(pageSize)
            .Select(v => new DashboardStockRow(v.ProductId, v.ProductName, v.VariantId, v.Size, v.Color, v.Stock, v.AvailableStock)).ToListAsync(ct);
        var recent = await period.OrderByDescending(o => o.CreatedAtUtc).ThenBy(o => o.Id).Take(5)
            .Select(o => new DashboardRecentOrder(o.Id, o.Number, o.CreatedAtUtc, o.PaymentMethod, o.PaymentStatus, o.FulfillmentStatus, o.Total, o.Currency)).ToListAsync(ct);
        return new(from, to, now, new(created, paid, paidTotal, refunded, refundedTotal, excluded, cardTests),
            new(pending, waiting, preparing, shipped, resolutions), new(query.StockThreshold, lowCount, outCount, missingVariants, query.StockPage, pageSize, rows), recent);
    }
}
