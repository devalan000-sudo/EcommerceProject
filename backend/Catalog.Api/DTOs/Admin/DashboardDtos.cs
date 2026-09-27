using System.ComponentModel.DataAnnotations;
using Catalog.Api.Entities;

namespace Catalog.Api.DTOs;

public class DashboardQuery : IValidatableObject
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(0, 1000000)] public int StockThreshold { get; set; } = 5;
    [Range(1, 100000)] public int StockPage { get; set; } = 1;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var to = To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = From ?? to.AddDays(-Math.Min(29, to.DayNumber));
        if (to == DateOnly.MaxValue || from > to || to.DayNumber - from.DayNumber > 365)
            yield return new("Selecciona un rango válido de hasta 366 días. La fecha final máxima es 9999-12-30.");
    }
}
public record DashboardSales(int OrdersCreated, int PaidOrders, decimal PaidTotal, int RefundedOrders,
    decimal RefundedTotal, int ExcludedAmounts, int CardTestOrders);
public record DashboardWork(int PendingPayment, int AwaitingPreparation, int Preparing, int Shipped, int Resolutions);
public record DashboardStockRow(int ProductId, string ProductName, int VariantId, string Size, string Color, int Stock, int AvailableStock);
public record DashboardStock(int Threshold, int TotalCount, int OutOfStockCount, int ProductsWithoutVariants,
    int Page, int PageSize, List<DashboardStockRow> Items);
public record DashboardRecentOrder(Guid Id, string Number, DateTime CreatedAtUtc, PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus, FulfillmentStatus FulfillmentStatus, decimal? Total, string Currency);
public record DashboardResponse(DateOnly From, DateOnly To, DateTime GeneratedAtUtc, DashboardSales Sales,
    DashboardWork Work, DashboardStock Inventory, List<DashboardRecentOrder> RecentOrders);
