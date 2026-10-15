using System.ComponentModel.DataAnnotations;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class DashboardService(CatalogDbContext db)
{
    private const int StockPageSize = 20;

    public async Task<DashboardResponse> GetAsync(DashboardQuery query, CancellationToken ct)
    {
        Validator.ValidateObject(query, new ValidationContext(query), true);
        var period = ResolvePeriod(query);
        var sales = await GetSalesAsync(period, ct);
        var work = await GetWorkAsync(ct);
        var inventory = await GetInventoryAsync(query, period.Now, ct);
        var recentOrders = await GetRecentOrdersAsync(period, ct);
        return new(period.From, period.To, period.Now, sales, work, inventory, recentOrders);
    }

    private static DashboardPeriod ResolvePeriod(DashboardQuery query)
    {
        var now = DateTime.UtcNow;
        var to = query.To ?? DateOnly.FromDateTime(now);
        var from = query.From ?? to.AddDays(-Math.Min(29, to.DayNumber));
        return new(from, to, now, from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    private async Task<DashboardSales> GetSalesAsync(DashboardPeriod period, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking().Where(o => o.CreatedAtUtc >= period.Start && o.CreatedAtUtc < period.End);
        var created = await orders.CountAsync(ct);
        int paid = 0, refunded = 0, excluded = 0, cardTests = 0;
        decimal paidTotal = 0, refundedTotal = 0;
        await foreach (var order in orders.Where(o => o.PaymentStatus == PaymentStatus.Paid || o.PaymentStatus == PaymentStatus.Refunded)
            .Select(o => new { o.PaymentStatus, o.Total, o.Currency, o.PaymentMethod }).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (order.Currency != "MXN" || !order.Total.HasValue) { excluded++; continue; }
            if (order.PaymentStatus == PaymentStatus.Paid) { paid++; paidTotal += order.Total.Value; }
            else { refunded++; refundedTotal += order.Total.Value; }
            if (order.PaymentMethod == PaymentMethod.Card) cardTests++;
        }
        var partials = await db.ReturnRefunds.AsNoTracking().Where(r => r.Status == ReturnRefundStatus.Succeeded &&
            r.ReturnRequest.Order.CreatedAtUtc >= period.Start && r.ReturnRequest.Order.CreatedAtUtc < period.End &&
            r.ReturnRequest.Order.Currency == "MXN" && r.ReturnRequest.Order.Total != null)
            .Select(r => new { r.Amount, r.ReturnRequest.OrderId }).ToListAsync(ct);
        paidTotal -= partials.Sum(r => r.Amount);
        refundedTotal += partials.Sum(r => r.Amount);
        refunded += partials.Select(r => r.OrderId).Distinct().Count();
        return new(created, paid, paidTotal, refunded, refundedTotal, excluded, cardTests);
    }

    private async Task<DashboardWork> GetWorkAsync(CancellationToken ct)
    {
        var all = db.Orders.AsNoTracking();
        var open = all.Where(o => o.FulfillmentStatus != FulfillmentStatus.Cancelled && o.FulfillmentStatus != FulfillmentStatus.Delivered);
        var actionable = open.Where(o => !o.CancellationPending && o.RefundStatus == null);
        var pending = await all.CountAsync(o => o.PaymentStatus == PaymentStatus.Pending && o.FulfillmentStatus != FulfillmentStatus.Cancelled, ct);
        var waiting = await actionable.CountAsync(o => o.PaymentStatus == PaymentStatus.Paid && o.FulfillmentStatus == FulfillmentStatus.PendingConfirmation, ct);
        var preparing = await actionable.CountAsync(o => o.FulfillmentStatus == FulfillmentStatus.Preparing, ct);
        var shipped = await actionable.CountAsync(o => o.FulfillmentStatus == FulfillmentStatus.Shipped, ct);
        var resolutions = await all.CountAsync(o => o.CancellationPending || (o.RefundStatus != null && o.RefundStatus != "Succeeded") ||
            (o.RefundStatus == "Succeeded" && o.RefundRestock && o.RestockedAtUtc == null) ||
            db.ReturnRefunds.Any(r => r.ReturnRequest.OrderId == o.Id && (r.Status == ReturnRefundStatus.Pending || r.Status == ReturnRefundStatus.NeedsReview)), ct);
        return new(pending, waiting, preparing, shipped, resolutions);
    }

    private async Task<DashboardStock> GetInventoryAsync(DashboardQuery query, DateTime now, CancellationToken ct)
    {
        var available = db.ProductVariants.AsNoTracking().Where(v => v.IsActive && v.Product.IsActive).Select(v => new
        {
            v.ProductId, ProductName = v.Product.Name, VariantId = v.Id, v.Size, v.Color, v.Stock,
            AvailableStock = v.Stock - (v.Reservations.Where(r => !r.Released && (r.ExpiresAtUtc > now || r.Order.StripeHold)).Sum(r => (int?)r.Quantity) ?? 0)
        });
        var low = available.Where(v => v.AvailableStock <= query.StockThreshold);
        var lowCount = await low.CountAsync(ct);
        var outCount = await available.CountAsync(v => v.AvailableStock <= 0, ct);
        var missingVariants = await db.Products.AsNoTracking().CountAsync(p => p.IsActive && !p.Variants.Any(v => v.IsActive), ct);
        var rows = await low.OrderBy(v => v.AvailableStock).ThenBy(v => v.ProductId).ThenBy(v => v.VariantId)
            .Skip((query.StockPage - 1) * StockPageSize).Take(StockPageSize)
            .Select(v => new DashboardStockRow(v.ProductId, v.ProductName, v.VariantId, v.Size, v.Color, v.Stock, v.AvailableStock)).ToListAsync(ct);
        return new(query.StockThreshold, lowCount, outCount, missingVariants, query.StockPage, StockPageSize, rows);
    }

    private Task<List<DashboardRecentOrder>> GetRecentOrdersAsync(DashboardPeriod period, CancellationToken ct) =>
        db.Orders.AsNoTracking().Where(o => o.CreatedAtUtc >= period.Start && o.CreatedAtUtc < period.End)
            .OrderByDescending(o => o.CreatedAtUtc).ThenBy(o => o.Id).Take(5)
            .Select(o => new DashboardRecentOrder(o.Id, o.Number, o.CreatedAtUtc, o.PaymentMethod, o.PaymentStatus, o.FulfillmentStatus, o.Total, o.Currency)).ToListAsync(ct);

    private sealed record DashboardPeriod(DateOnly From, DateOnly To, DateTime Now, DateTime Start, DateTime End);
}
