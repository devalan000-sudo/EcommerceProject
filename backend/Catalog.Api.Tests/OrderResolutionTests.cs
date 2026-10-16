using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

public class OrderResolutionTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly OrderService orders;
    private readonly OrderResolutionService service;
    private readonly PaymentService payments;
    private readonly FakeStripe handler = new();
    private readonly HttpClient http;
    private readonly ProductVariant variant;
    private readonly Order order;
    public OrderResolutionTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        var customer = new Customer { Email = "resolution@example.com", NormalizedEmail = "RESOLUTION@EXAMPLE.COM" };
        var product = new Product { Name = "Playera", Price = 50, CategoryId = 1 };
        db.AddRange(customer, product); db.SaveChanges();
        variant = new ProductVariant { ProductId = product.Id, Size = "M", Color = "NEGRO", Stock = 3, IsActive = true };
        db.Add(variant); db.SaveChanges();
        order = new Order
        {
            Id = Guid.NewGuid(),
            Number = Guid.NewGuid().ToString(),
            CustomerId = customer.Id,
            PaymentMethod = PaymentMethod.WhatsApp,
            PaymentStatus = PaymentStatus.Paid,
            Subtotal = 100,
            ShippingCost = 10,
            Total = 110,
            ShippingName = "Local",
            ManualPaymentConfirmedAtUtc = DateTime.UtcNow,
            Items = [new() { ProductId = product.Id, VariantId = variant.Id, Size = "M", Color = "NEGRO", ProductName = "Playera", Quantity = 2, UnitPrice = 50, LineTotal = 100 }]
        };
        db.Add(order); db.SaveChanges(); handler.OrderId = order.Id;
        http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Stripe:SecretKey"] = "sk_test_unit_fixture", ["Stripe:WebhookSecret"] = "whsec_unit_fixture" }).Build();
        var gateway = new StripeGateway(http, config);
        orders = new(db); payments = new(db, new InventoryService(db), gateway); service = new(db, orders, payments, gateway);
    }
    private RefundOrderDto Request(bool restock = true) => new() { RequestId = Guid.NewGuid(), Version = Guid.Empty, Reason = "Solicitud del cliente", Restock = restock, ManualRefundConfirmed = true };
    private async Task Card()
    { order.PaymentMethod = PaymentMethod.Card; order.StripeSessionId = "cs_test_fixture"; await db.SaveChangesAsync(); }
    private async Task<Order> Saved() => await db.Orders.AsNoTracking().SingleAsync();
    private async Task<int> Stock() => (await db.ProductVariants.AsNoTracking().SingleAsync()).Stock;

    [Theory]
    [InlineData(true, 5)]
    [InlineData(false, 3)]
    public async Task ManualRefundRecordsExactlyOnceWithOptionalRestock(bool restock, int expected)
    {
        var request = Request(restock);
        await service.RefundAsync(order.Id, 9, request, default);
        await service.RefundAsync(order.Id, 9, request, default);
        Assert.Equal(PaymentStatus.Refunded, (await Saved()).PaymentStatus);
        Assert.Equal(FulfillmentStatus.Cancelled, (await Saved()).FulfillmentStatus);
        Assert.Equal(9, (await Saved()).RefundAdminId); Assert.Equal(expected, await Stock());
        await Assert.ThrowsAsync<ValidationException>(() => service.RefundAsync(order.Id, 9, Request(), default));
        if (restock) { await service.RetryRestockAsync(order.Id, default); Assert.Equal(expected, await Stock()); }
        Assert.Equal(0, handler.PostCount);
    }
    [Fact]
    public async Task CancelUnpaidDoesNotAddStockAndIsIdempotent()
    {
        order.PaymentStatus = PaymentStatus.Pending; variant.Stock = 5; await db.SaveChangesAsync();
        var dto = new CancelOrderDto { Version = Guid.Empty, Reason = "Ya no lo necesita" };
        await service.CancelAsync(order.Id, 9, dto, default); await service.CancelAsync(order.Id, 9, dto, default);
        Assert.Equal(5, await Stock()); Assert.Equal(FulfillmentStatus.Cancelled, (await Saved()).FulfillmentStatus);
        Assert.Equal(9, (await Saved()).CancellationAdminId);
    }
    [Fact]
    public async Task RejectsPaidCancellationAndUnconfirmedManualRefund()
    {
        await Assert.ThrowsAsync<ValidationException>(() => service.CancelAsync(order.Id, 9, new() { Version = Guid.Empty, Reason = "Cancelar" }, default));
        var request = Request(); request.ManualRefundConfirmed = false;
        await Assert.ThrowsAsync<ValidationException>(() => service.RefundAsync(order.Id, 9, request, default));
        request = Request(); request.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.RefundAsync(order.Id, 9, request, default));
        Assert.Equal(3, await Stock()); Assert.Null((await Saved()).RefundRequestId);
    }
    [Theory]
    [InlineData(FulfillmentStatus.Shipped)]
    [InlineData(FulfillmentStatus.Delivered)]
    public async Task RejectsRefundAfterDispatch(FulfillmentStatus status)
    {
        order.FulfillmentStatus = status; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(() => service.RefundAsync(order.Id, 9, Request(), default));
        Assert.Null((await Saved()).RefundRequestId);
    }
    [Fact]
    public async Task StripeRefundWithoutLiveModeFieldIsValidatedAndLatePaymentDoesNotReopen()
    {
        await Card(); var request = Request();
        await service.RefundAsync(order.Id, 9, request, default);
        await service.RefundAsync(order.Id, 9, request, default);
        Assert.Equal(1, handler.PostCount); Assert.Equal(5, await Stock());
        Assert.Contains("amount=11000", handler.Payloads[0]); Assert.Contains("payment_intent=pi_fixture", handler.Payloads[0]);
        await payments.ApplySessionAsync(order.Id, handler.Session(), default);
        Assert.Equal(PaymentStatus.Refunded, (await Saved()).PaymentStatus); Assert.Equal(5, await Stock());
    }
    [Fact]
    public async Task PendingRefundDoesNotRestockOrMarkRefundedUntilSucceeded()
    {
        await Card(); handler.RefundStatus = "pending";
        await service.RefundAsync(order.Id, 9, Request(), default);
        Assert.Equal(PaymentStatus.Paid, (await Saved()).PaymentStatus); Assert.Equal(3, await Stock());
        var version = (await Saved()).FulfillmentVersion;
        await Assert.ThrowsAsync<ValidationException>(() => new AdminOrderService(db, orders).UpdateAsync(order.Id, new() { Status = FulfillmentStatus.Preparing, Version = version }, default));
        handler.RefundStatus = "succeeded";
        await service.ReconcileRefundAsync(order.Id, default); await service.ReconcileRefundAsync(order.Id, default);
        Assert.Equal(1, handler.PostCount); Assert.Equal(5, await Stock()); Assert.Equal(PaymentStatus.Refunded, (await Saved()).PaymentStatus);
    }
    [Fact]
    public async Task UncertainRequestUsesSameProviderKeyOnRetry()
    {
        await Card(); handler.Fail = true; var request = Request();
        await Assert.ThrowsAsync<HttpRequestException>(() => service.RefundAsync(order.Id, 9, request, default));
        Assert.Equal("Pending", (await Saved()).RefundStatus); Assert.Equal(3, await Stock());
        handler.Fail = false; await service.RefundAsync(order.Id, 9, request, default);
        Assert.Equal(2, handler.PostCount); Assert.Single(handler.Keys.Distinct()); Assert.Single(handler.Payloads.Distinct());
        Assert.Equal(5, await Stock());
    }
    [Theory]
    [InlineData("failed", "Failed")]
    [InlineData("requires_action", "NeedsReview")]
    public async Task NonSuccessfulRefundNeverChangesMoneyOrStock(string stripeStatus, string expected)
    {
        await Card(); handler.RefundStatus = stripeStatus;
        await service.RefundAsync(order.Id, 9, Request(), default);
        Assert.Equal(expected, (await Saved()).RefundStatus); Assert.Equal(PaymentStatus.Paid, (await Saved()).PaymentStatus); Assert.Equal(3, await Stock());
    }
    [Fact]
    public async Task WrongAmountCannotConfirmRefund()
    {
        await Card(); handler.Amount = 1;
        await Assert.ThrowsAsync<ValidationException>(() => service.RefundAsync(order.Id, 9, Request(), default));
        Assert.Equal(PaymentStatus.Paid, (await Saved()).PaymentStatus); Assert.Equal(3, await Stock());
    }
    [Fact]
    public async Task SuccessfulRefundWithChangedVariantLeavesRestockForReview()
    {
        await Card(); handler.RefundStatus = "pending";
        await service.RefundAsync(order.Id, 9, Request(), default);
        await db.ProductVariants.ExecuteUpdateAsync(u => u.SetProperty(v => v.Color, "ROJO"));
        handler.RefundStatus = "succeeded"; await service.ReconcileRefundAsync(order.Id, default);
        Assert.Equal(PaymentStatus.Refunded, (await Saved()).PaymentStatus); Assert.Null((await Saved()).RestockedAtUtc); Assert.Equal(3, await Stock());
        await Assert.ThrowsAsync<ValidationException>(() => service.RetryRestockAsync(order.Id, default));
        db.ChangeTracker.Clear(); await db.ProductVariants.ExecuteUpdateAsync(u => u.SetProperty(v => v.Color, "NEGRO"));
        await service.RetryRestockAsync(order.Id, default); await service.RetryRestockAsync(order.Id, default);
        Assert.Equal(5, await Stock());
    }
    [Fact]
    public async Task CancellationRacePreservesPaymentAndDoesNotRefundAutomatically()
    {
        await Card(); order.PaymentStatus = PaymentStatus.Pending; order.StripeHold = true; variant.Stock = 5;
        db.InventoryReservations.Add(new() { OrderId = order.Id, VariantId = variant.Id, Quantity = 2, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15) }); await db.SaveChangesAsync();
        var result = await service.CancelAsync(order.Id, 9, new() { Version = Guid.Empty, Reason = "Cancelar" }, default);
        Assert.Equal(PaymentStatus.Paid, result!.PaymentStatus); Assert.False(result.CancellationPending); Assert.Equal(3, await Stock()); Assert.Equal(0, handler.PostCount);
    }
    [Fact]
    public async Task LostRefundBeyondRetryWindowRequiresReview()
    {
        await Card(); handler.Fail = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => service.RefundAsync(order.Id, 9, Request(), default));
        await db.Orders.ExecuteUpdateAsync(u => u.SetProperty(o => o.RefundRequestedAtUtc, DateTime.UtcNow.AddHours(-25)));
        handler.Fail = false; await service.ReconcileRefundAsync(order.Id, default);
        Assert.Equal("NeedsReview", (await Saved()).RefundStatus); Assert.Equal(1, handler.PostCount); Assert.Equal(3, await Stock());
    }
    [Fact]
    public async Task CancellationOutageKeepsReservationUntilStripeConfirmsClosure()
    {
        await Card(); order.PaymentStatus = PaymentStatus.Pending; order.StripeHold = true; variant.Stock = 5;
        db.InventoryReservations.Add(new() { OrderId = order.Id, VariantId = variant.Id, Quantity = 2, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15) }); await db.SaveChangesAsync();
        handler.FailCheckout = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => service.CancelAsync(order.Id, 9, new() { Version = Guid.Empty, Reason = "Cancelar" }, default));
        Assert.True((await Saved()).CancellationPending); Assert.True((await Saved()).StripeHold);
        Assert.Equal(3, (await InventoryService.Query(db, variant.ProductId, false, DateTime.UtcNow).SingleAsync()).AvailableStock);
        handler.FailCheckout = false; handler.CheckoutStatus = "open";
        await payments.ReconcileAsync(order.Id, default);
        Assert.False((await Saved()).StripeHold); Assert.Equal(FulfillmentStatus.Cancelled, (await Saved()).FulfillmentStatus); Assert.Equal(5, await Stock());
    }
    [Fact]
    public void ResolutionEndpointsRequireAdmin()
    {
        var auth = Assert.Single(typeof(Catalog.Api.Controllers.OrderResolutionController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal("Admin", auth.Roles);
    }
    public void Dispose() { http.Dispose(); db.Dispose(); connection.Dispose(); }

    private class FakeStripe : HttpMessageHandler
    {
        public Guid OrderId;
        public string RefundStatus = "succeeded";
        public bool Fail;
        public bool FailCheckout;
        public string CheckoutStatus = "complete";
        public long Amount = 11000;
        public int PostCount;
        public List<string> Keys = []; public List<string> Payloads = [];
        private string requestId = "";
        public JsonElement Session() => JsonSerializer.SerializeToElement(new { id = "cs_test_fixture", livemode = false, mode = "payment", status = CheckoutStatus, payment_status = CheckoutStatus == "complete" ? "paid" : "unpaid", payment_intent = "pi_fixture", client_reference_id = OrderId.ToString(), metadata = new { order_id = OrderId.ToString() }, amount_total = 11000, currency = "mxn" });
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.Contains("checkout"))
            {
                if (FailCheckout) return new(HttpStatusCode.ServiceUnavailable);
                if (request.RequestUri.AbsolutePath.EndsWith("/expire")) CheckoutStatus = "expired";
                return Json(Session());
            }
            if (request.Method == HttpMethod.Post)
            {
                PostCount++; Keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
                var body = await request.Content!.ReadAsStringAsync(ct); Payloads.Add(body);
                var values = body.Split('&').Select(v => v.Split('=', 2)).ToDictionary(v => Uri.UnescapeDataString(v[0]), v => Uri.UnescapeDataString(v[1]));
                requestId = values["metadata[request_id]"];
                if (Fail) return new(HttpStatusCode.ServiceUnavailable);
            }
            return Json(JsonSerializer.SerializeToElement(new { id = "re_fixture", @object = "refund", amount = Amount, currency = "mxn", payment_intent = "pi_fixture", status = RefundStatus, metadata = new { order_id = OrderId.ToString(), request_id = requestId } }));
        }
        private static HttpResponseMessage Json(JsonElement element) => new(HttpStatusCode.OK) { Content = new StringContent(element.GetRawText(), Encoding.UTF8, "application/json") };
    }
}
