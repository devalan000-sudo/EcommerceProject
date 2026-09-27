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

public class ReturnRefundTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<CatalogDbContext> options;
    private readonly FakeStripe handler = new();
    private readonly HttpClient http;
    private readonly StripeGateway stripe;
    private readonly Guid id;
    private readonly int itemId;
    public ReturnRefundTests()
    {
        connection.Open(); options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options;
        using var db = new CatalogDbContext(options); db.Database.EnsureCreated();
        var product = new Product { Name = "Playera", CategoryId = 1, Price = 900 };
        var variant = new ProductVariant { Product = product, Size = "M", Color = "Rojo", Stock = 12 };
        db.Add(variant); db.SaveChanges();
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Number = "REFUND-TEST",
            CreatedAtUtc = DateTime.UtcNow,
            Customer = new Customer { Email = "refund@example.test", NormalizedEmail = "REFUND@EXAMPLE.TEST" },
            PaymentMethod = PaymentMethod.WhatsApp,
            PaymentStatus = PaymentStatus.Paid,
            FulfillmentStatus = FulfillmentStatus.Delivered,
            Subtotal = 300,
            Total = 310,
            ShippingCost = 10,
            ManualPaymentConfirmedAtUtc = DateTime.UtcNow,
            StripeSessionId = "cs_fixture",
            Items = [new() { ProductId = product.Id, VariantId = variant.Id, ProductName = "Playera", Quantity = 3, UnitPrice = 100, LineTotal = 300 }]
        };
        db.Add(order); db.SaveChanges(); itemId = order.Items[0].Id; handler.OrderId = order.Id;
        var request = new ReturnRequest
        {
            Order = order,
            Status = ReturnStatus.Received,
            ClientRequestId = Guid.NewGuid(),
            Items = [new() { OrderItemId = itemId, Quantity = 3, ReceivedQuantity = 3, RestockedQuantity = 2 }]
        };
        db.Add(request); db.SaveChanges(); id = request.Id;
        http = new(handler); stripe = new(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = "sk_test_unit_fixture",
            ["Stripe:WebhookSecret"] = "whsec_unit_fixture"
        }).Build());
    }
    private ReturnRefundCreateDto Request(int quantity = 1)
    {
        using var db = new CatalogDbContext(options);
        return new()
        {
            RequestId = Guid.NewGuid(),
            Version = db.ReturnRequests.Single().Version,
            Note = "Devolución de artículos",
            ManualReference = "Comprobante de prueba",
            ManualRefundConfirmed = true,
            ExpectedAmount = quantity * 100,
            Items = [new() { OrderItemId = itemId, Quantity = quantity }]
        };
    }
    private async Task<ReturnRefundDto> Create(ReturnRefundCreateDto dto)
    { await using var db = new CatalogDbContext(options); return (await new ReturnRefundService(db, stripe).CreateAsync(id, 7, dto, default))!; }
    private async Task<ReturnRefundDto?> Refresh(Guid refundId, string? stripeId = null)
    { await using var db = new CatalogDbContext(options); return await new ReturnRefundService(db, stripe).ReconcileAsync(refundId, stripeId, default); }
    private void Card() { using var db = new CatalogDbContext(options); db.Orders.Single().PaymentMethod = PaymentMethod.Card; db.SaveChanges(); }

    [Fact]
    public async Task ManualUsesOriginalPricesAndReplayDoesNotDuplicateMoneyOrInventory()
    {
        var dto = Request(); var first = await Create(dto); var retry = await Create(dto);
        Assert.Equal(first.Id, retry.Id); Assert.Equal(100m, first.Amount); Assert.Equal(ReturnRefundStatus.Succeeded, first.Status);
        await Create(Request(2));
        using var db = new CatalogDbContext(options);
        Assert.Equal(300m, db.ReturnRefunds.AsEnumerable().Sum(r => r.Amount)); Assert.Equal(12, db.ProductVariants.Single().Stock);
        Assert.Equal(2, db.EmailOutbox.Count(m => m.Kind == "ReturnRefundSucceeded"));
        Assert.Equal(2, db.ReturnRequestItems.Single().RestockedQuantity); Assert.Equal(PaymentStatus.Paid, db.Orders.Single().PaymentStatus);
        Assert.Equal(FulfillmentStatus.Delivered, db.Orders.Single().FulfillmentStatus); Assert.Null(db.Orders.Single().RefundRequestId);
        Assert.Equal(0, handler.Posts);
        var dashboard = await new DashboardService(db).GetAsync(new(), default);
        Assert.Equal(10m, dashboard.Sales.PaidTotal); Assert.Equal(300m, dashboard.Sales.RefundedTotal); Assert.Equal(1, dashboard.Sales.RefundedOrders);
    }
    [Fact]
    public async Task RejectsUnconfirmedManualStaleChangedAndExcessiveRequests()
    {
        var dto = Request(); dto.ManualRefundConfirmed = false; await Assert.ThrowsAsync<ValidationException>(() => Create(dto));
        dto = Request(); dto.ExpectedAmount = 1; await Assert.ThrowsAsync<ValidationException>(() => Create(dto));
        var stale = Request(); dto = Request(); await Create(dto);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Create(stale));
        dto.Note = "Cambio"; await Assert.ThrowsAsync<ValidationException>(() => Create(dto));
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request(3)));
    }
    [Fact]
    public async Task CannotRefundUnreceivedOrExceedWholeOrderBalance()
    {
        using (var db = new CatalogDbContext(options)) { var item = db.ReturnRequestItems.Single(); item.ReceivedQuantity = 2; db.SaveChanges(); }
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request(3)));
        using (var db = new CatalogDbContext(options)) { db.Orders.Single().Total = 50; db.SaveChanges(); }
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request()));
    }
    [Fact]
    public async Task PendingStripeHoldsQuantitiesAndPollingConfirmsWithoutAnotherPost()
    {
        Card(); handler.Status = "pending"; var result = await Create(Request(3));
        Assert.Equal(ReturnRefundStatus.Pending, result.Status);
        using (var db = new CatalogDbContext(options)) Assert.Empty(db.EmailOutbox.Where(m => m.Kind == "ReturnRefundSucceeded"));
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request()));
        handler.Status = "succeeded"; var confirmed = await Refresh(result.Id);
        Assert.Equal(ReturnRefundStatus.Succeeded, confirmed!.Status); Assert.NotNull(confirmed.CompletedAtUtc);
        await Refresh(result.Id); Assert.Equal(1, handler.Posts);
        using (var db = new CatalogDbContext(options)) Assert.Single(db.EmailOutbox.Where(m => m.Kind == "ReturnRefundSucceeded"));
    }
    [Fact]
    public async Task NetworkLossUsesSameStripeKeyAndPendingRecord()
    {
        Card(); handler.FailAfterPost = true; var dto = Request();
        await Assert.ThrowsAsync<HttpRequestException>(() => Create(dto));
        using (var db = new CatalogDbContext(options)) Assert.Equal(ReturnRefundStatus.Pending, db.ReturnRefunds.Single().Status);
        handler.FailAfterPost = false; var result = await Create(dto);
        Assert.Equal(ReturnRefundStatus.Succeeded, result.Status); Assert.Single(handler.Keys.Distinct());
        using var verify = new CatalogDbContext(options); Assert.Single(verify.ReturnRefunds);
    }
    [Fact]
    public async Task UnknownOldResultStopsCreatingAndCanRecoverVerifiedStripeId()
    {
        Card(); handler.FailAfterPost = true; await Assert.ThrowsAsync<HttpRequestException>(() => Create(Request()));
        Guid refundId;
        using (var db = new CatalogDbContext(options)) { var row = db.ReturnRefunds.Single(); row.CreatedAtUtc = DateTime.UtcNow.AddDays(-2); refundId = row.Id; db.SaveChanges(); }
        handler.FailAfterPost = false; Assert.Equal(ReturnRefundStatus.NeedsReview, (await Refresh(refundId))!.Status);
        Assert.Equal(1, handler.Posts);
        Assert.Equal(ReturnRefundStatus.Succeeded, (await Refresh(refundId, "re_fixture"))!.Status); Assert.Equal(1, handler.Posts);
    }
    [Fact]
    public async Task MismatchedProviderAmountNeverConfirmsAndKeepsBalanceReserved()
    {
        Card(); handler.BadAmount = true; await Assert.ThrowsAsync<ValidationException>(() => Create(Request(3)));
        using (var db = new CatalogDbContext(options)) Assert.Equal(ReturnRefundStatus.Pending, db.ReturnRefunds.Single().Status);
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request()));
    }
    [Fact]
    public async Task ConfirmedFailureReleasesQuantitiesForNewRequest()
    {
        Card(); handler.Status = "failed"; var failed = await Create(Request(3)); Assert.Equal(ReturnRefundStatus.Failed, failed.Status);
        handler.Status = "succeeded"; handler.RefundId = "re_second";
        var success = await Create(Request(3)); Assert.Equal(ReturnRefundStatus.Succeeded, success.Status);
    }
    [Fact]
    public void RefundEndpointsRequireAdmin() => Assert.Contains(typeof(Catalog.Api.Controllers.ReturnRefundController)
        .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(), a => a.Roles == "Admin");
    public void Dispose() { http.Dispose(); connection.Dispose(); }

    private class FakeStripe : HttpMessageHandler
    {
        public Guid OrderId;
        public string Status = "succeeded", RefundId = "re_fixture";
        public bool FailAfterPost, BadAmount;
        public int Posts;
        public List<string> Keys = [];
        private Dictionary<string, string> values = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.Contains("checkout")) return Json(new
            {
                id = "cs_fixture",
                livemode = false,
                status = "complete",
                payment_status = "paid",
                amount_total = 31000,
                currency = "mxn",
                client_reference_id = OrderId.ToString(),
                metadata = new { order_id = OrderId.ToString() },
                payment_intent = "pi_fixture"
            });
            if (request.Method == HttpMethod.Post)
            {
                Posts++; Keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
                var body = await request.Content!.ReadAsStringAsync(ct);
                values = body.Split('&').Select(s => s.Split('=', 2)).ToDictionary(s => Uri.UnescapeDataString(s[0]), s => Uri.UnescapeDataString(s[1]));
                if (FailAfterPost) return new(HttpStatusCode.ServiceUnavailable);
            }
            return Json(new
            {
                id = RefundId,
                @object = "refund",
                amount = BadAmount ? 1 : long.Parse(values["amount"]),
                currency = "mxn",
                payment_intent = "pi_fixture",
                status = Status,
                metadata = new { order_id = OrderId.ToString(), return_refund_id = values["metadata[return_refund_id]"] }
            });
        }
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
