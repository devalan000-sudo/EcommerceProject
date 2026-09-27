using System.ComponentModel.DataAnnotations;
using Catalog.Api.Controllers;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class DashboardTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly Customer customer = new() { Email = "qa@example.test", NormalizedEmail = "QA@EXAMPLE.TEST" };
    private static readonly DateTime start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DashboardQuery Query() => new() { From = new(2026, 9, 1), To = new(2026, 9, 30) };
    public DashboardTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated(); db.Customers.Add(customer); db.SaveChanges();
    }
    private Order Order(PaymentStatus status, decimal? total, DateTime? date = null, FulfillmentStatus fulfillment = FulfillmentStatus.PendingConfirmation)
    {
        var id = Guid.NewGuid();
        var order = new Order
        {
            Id = id,
            Number = $"QA-{id:N}",
            CustomerId = customer.Id,
            CreatedAtUtc = date ?? start,
            PaymentMethod = PaymentMethod.WhatsApp,
            PaymentStatus = status,
            Total = total,
            Subtotal = total ?? 0,
            FulfillmentStatus = fulfillment
        };
        db.Orders.Add(order); return order;
    }
    [Fact]
    public async Task SalesUseCreationWindowAndCurrentStatusWithoutMixingRefundsOrCurrencies()
    {
        Order(PaymentStatus.Paid, 123.45m); var card = Order(PaymentStatus.Paid, 10.10m); card.PaymentMethod = PaymentMethod.Card;
        Order(PaymentStatus.Refunded, 50m); Order(PaymentStatus.Pending, 999m); Order(PaymentStatus.Failed, 555m);
        Order(PaymentStatus.Paid, 800m, start.AddTicks(-1)); Order(PaymentStatus.Paid, 900m, start.AddMonths(1));
        Order(PaymentStatus.Paid, null); Order(PaymentStatus.Paid, 30m).Currency = "USD";
        await db.SaveChangesAsync();
        var result = await new DashboardService(db).GetAsync(Query(), default);
        Assert.Equal(7, result.Sales.OrdersCreated); Assert.Equal(2, result.Sales.PaidOrders);
        Assert.Equal(133.55m, result.Sales.PaidTotal); Assert.Equal(50m, result.Sales.RefundedTotal);
        Assert.Equal(1, result.Sales.RefundedOrders); Assert.Equal(2, result.Sales.ExcludedAmounts); Assert.Equal(1, result.Sales.CardTestOrders);
        Assert.Equal(5, result.RecentOrders.Count);
    }
    [Fact]
    public async Task WorkIncludesOldOutstandingOrdersAndRefundRestockIssues()
    {
        Order(PaymentStatus.Pending, null, start.AddYears(-1), FulfillmentStatus.Delivered);
        Order(PaymentStatus.Pending, 100, start, FulfillmentStatus.Cancelled);
        Order(PaymentStatus.Paid, 100, start.AddYears(-1));
        Order(PaymentStatus.Paid, 100, start, FulfillmentStatus.Preparing);
        Order(PaymentStatus.Paid, 100, start, FulfillmentStatus.Shipped);
        var resolving = Order(PaymentStatus.Paid, 100); resolving.RefundStatus = "Pending";
        var restock = Order(PaymentStatus.Refunded, 100, start, FulfillmentStatus.Cancelled); restock.RefundStatus = "Succeeded"; restock.RefundRestock = true;
        await db.SaveChangesAsync();
        var result = await new DashboardService(db).GetAsync(Query(), default);
        Assert.Equal(1, result.Work.PendingPayment); Assert.Equal(1, result.Work.AwaitingPreparation);
        Assert.Equal(1, result.Work.Preparing); Assert.Equal(1, result.Work.Shipped); Assert.Equal(2, result.Work.Resolutions);
    }
    [Fact]
    public async Task StockDeductsActiveAndHeldReservationsButIgnoresReleasedAndExpired()
    {
        var product = new Product { Name = "Playera", CategoryId = 1, Price = 100 };
        var variant = new ProductVariant { Product = product, Size = "M", Color = "Rojo", Stock = 10 };
        db.ProductVariants.Add(variant);
        db.ProductVariants.Add(new() { Product = product, Size = "L", Color = "Rojo", Stock = 0, IsActive = false });
        db.Products.Add(new() { Name = "Sin variantes", CategoryId = 1, Price = 10 });
        db.ProductVariants.Add(new() { Product = new() { Name = "Oculto", CategoryId = 1, Price = 10, IsActive = false }, Size = "M", Color = "Azul", Stock = 0 });
        var active = Order(PaymentStatus.Pending, 100); var held = Order(PaymentStatus.Pending, 100); held.StripeHold = true; held.PaymentMethod = PaymentMethod.Card;
        db.InventoryReservations.AddRange(
            new() { Order = active, Variant = variant, Quantity = 3, ExpiresAtUtc = DateTime.UtcNow.AddHours(1) },
            new() { Order = held, Variant = variant, Quantity = 2, ExpiresAtUtc = DateTime.UtcNow.AddHours(-1) },
            new() { Order = Order(PaymentStatus.Pending, 100), Variant = variant, Quantity = 4, ExpiresAtUtc = DateTime.UtcNow.AddHours(-1) },
            new() { Order = Order(PaymentStatus.Pending, 100), Variant = variant, Quantity = 4, ExpiresAtUtc = DateTime.UtcNow.AddHours(1), Released = true });
        await db.SaveChangesAsync();
        var result = await new DashboardService(db).GetAsync(Query(), default);
        var row = Assert.Single(result.Inventory.Items); Assert.Equal(5, row.AvailableStock); Assert.Equal(10, row.Stock);
        Assert.Equal(1, result.Inventory.TotalCount); Assert.Equal(0, result.Inventory.OutOfStockCount); Assert.Equal(1, result.Inventory.ProductsWithoutVariants);
    }
    [Fact]
    public async Task StockPaginationDoesNotHideTotalAlertCount()
    {
        var product = new Product { Name = "Producto", CategoryId = 1, Price = 10 };
        for (var i = 0; i < 23; i++) db.ProductVariants.Add(new() { Product = product, Size = i.ToString(), Color = "Negro", Stock = 0 });
        await db.SaveChangesAsync(); var query = Query(); query.StockPage = 2;
        var result = await new DashboardService(db).GetAsync(query, default);
        Assert.Equal(23, result.Inventory.TotalCount); Assert.Equal(23, result.Inventory.OutOfStockCount); Assert.Equal(3, result.Inventory.Items.Count);
    }
    [Fact]
    public async Task EmptyStoreReturnsZeroInsteadOfInventedStatistics()
    {
        var result = await new DashboardService(db).GetAsync(Query(), default);
        Assert.Equal(0, result.Sales.PaidTotal); Assert.Equal(0, result.Sales.OrdersCreated);
        Assert.Empty(result.RecentOrders); Assert.Empty(result.Inventory.Items);
    }
    [Fact]
    public async Task InvalidRangesAndStockThresholdAreRejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => new DashboardService(db).GetAsync(new() { From = new(2020, 1, 1), To = new(2026, 1, 1) }, default));
        await Assert.ThrowsAsync<ValidationException>(() => new DashboardService(db).GetAsync(new() { To = DateOnly.MaxValue }, default));
        await Assert.ThrowsAsync<ValidationException>(() => new DashboardService(db).GetAsync(new() { StockThreshold = -1 }, default));
    }
    [Fact]
    public void EndpointIsRestrictedToAdmin()
    {
        var auth = Assert.Single(typeof(DashboardController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", auth.Roles);
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
