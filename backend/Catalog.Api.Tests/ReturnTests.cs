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

public class ReturnTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<CatalogDbContext> options;
    private readonly Guid orderId;
    private readonly int customerId;
    private readonly int itemId;
    public ReturnTests()
    {
        connection.Open(); options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options;
        using var db = new CatalogDbContext(options); db.Database.EnsureCreated();
        var customer = new Customer { Email = "return@example.test", NormalizedEmail = "RETURN@EXAMPLE.TEST" };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Number = "TEST-RETURN",
            Customer = customer,
            PaymentStatus = PaymentStatus.Paid,
            PaymentMethod = PaymentMethod.WhatsApp,
            FulfillmentStatus = FulfillmentStatus.Delivered,
            Subtotal = 200,
            ShippingCost = 10,
            Total = 210,
            Items = [new OrderItem { ProductName = "Playera", Size = "M", Color = "Rojo", Quantity = 2, UnitPrice = 100, LineTotal = 200 }]
        };
        db.Orders.Add(order); db.SaveChanges(); orderId = order.Id; customerId = customer.Id; itemId = order.Items[0].Id;
    }
    private ReturnCreateDto Request(int quantity = 1) => new() { RequestId = Guid.NewGuid(), Reason = "No me queda", Items = [new() { OrderItemId = itemId, Quantity = quantity }] };
    private async Task<ReturnResponseDto?> Create(ReturnCreateDto dto, int? owner = null)
    { await using var db = new CatalogDbContext(options); return await new ReturnService(db).CreateAsync(orderId, owner ?? customerId, dto, default); }
    private async Task<ReturnResponseDto?> Review(ReturnResponseDto request, ReturnStatus status = ReturnStatus.Approved)
    { await using var db = new CatalogDbContext(options); return await new ReturnService(db).ReviewAsync(request.Id, 7, new() { Version = request.Version, Status = status, DecisionNote = "Coordina la entrega con el negocio." }, default); }

    [Fact]
    public async Task SameRequestIsIdempotentAndChangedPayloadIsRejected()
    {
        var dto = Request(); var first = await Create(dto); var retry = await Create(dto);
        Assert.Equal(first!.Id, retry!.Id); dto.Reason = "Otro motivo";
        await Assert.ThrowsAsync<ValidationException>(() => Create(dto));
        using var db = new CatalogDbContext(options); Assert.Equal(1, db.ReturnRequests.Count());
    }
    [Fact]
    public async Task OwnershipIsRequiredForCreateOptionsAndList()
    {
        Assert.Null(await Create(Request(), customerId + 1));
        await Create(Request()); using var db = new CatalogDbContext(options); var service = new ReturnService(db);
        Assert.Null(await service.OptionsAsync(orderId, customerId + 1, default));
        Assert.Empty((await service.ListAsync(customerId + 1, orderId, null, 1, default)).Items);
        Assert.Single((await service.ListAsync(customerId, orderId, null, 1, default)).Items);
    }
    [Fact]
    public async Task PendingAndApprovedQuantitiesCannotBeRequestedTwice()
    {
        var first = (await Create(Request()))!; await Review(first);
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request(2)));
        await Create(Request());
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request()));
        using var db = new CatalogDbContext(options); var eligible = await new ReturnService(db).OptionsAsync(orderId, customerId, default);
        Assert.False(eligible!.CanRequest); Assert.Equal(0, Assert.Single(eligible.Items).Quantity);
    }
    [Fact]
    public async Task RejectedRequestReleasesQuantitiesAndStoresDecisionAudit()
    {
        var request = (await Create(Request(2)))!; var result = await Review(request, ReturnStatus.Rejected);
        Assert.Equal(ReturnStatus.Rejected, result!.Status); Assert.NotNull(result.ReviewedAtUtc);
        await Create(Request(2));
        using var db = new CatalogDbContext(options); Assert.Equal(7, db.ReturnRequests.Single(r => r.Id == request.Id).ReviewedByAdminId);
    }
    [Fact]
    public async Task ReviewCannotOverwriteAnotherDecisionOrChangeMoneyAndFulfillment()
    {
        using var before = new CatalogDbContext(options); var emailCount = before.EmailOutbox.Count();
        var request = (await Create(Request()))!; var approved = (await Review(request))!;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Review(request, ReturnStatus.Rejected));
        await Assert.ThrowsAsync<ValidationException>(() => Review(approved, ReturnStatus.Rejected));
        using var db = new CatalogDbContext(options); var order = db.Orders.Single();
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus); Assert.Equal(FulfillmentStatus.Delivered, order.FulfillmentStatus);
        Assert.Equal(210m, order.Total); Assert.Null(order.RefundRequestId); Assert.Null(order.RestockedAtUtc);
        Assert.Equal(emailCount + 1, db.EmailOutbox.Count());
        Assert.Single(db.EmailOutbox.Where(m => m.Kind == "ReturnApproved"));
    }
    [Theory]
    [InlineData(PaymentStatus.Pending, FulfillmentStatus.Delivered, false)]
    [InlineData(PaymentStatus.Refunded, FulfillmentStatus.Delivered, false)]
    [InlineData(PaymentStatus.Paid, FulfillmentStatus.Preparing, false)]
    [InlineData(PaymentStatus.Paid, FulfillmentStatus.Cancelled, false)]
    [InlineData(PaymentStatus.Paid, FulfillmentStatus.Shipped, true)]
    public async Task ChecksPaymentAndShipping(PaymentStatus payment, FulfillmentStatus fulfillment, bool eligible)
    {
        using (var db = new CatalogDbContext(options)) { var order = db.Orders.Single(); order.PaymentStatus = payment; order.FulfillmentStatus = fulfillment; db.SaveChanges(); }
        if (eligible) Assert.NotNull(await Create(Request()));
        else await Assert.ThrowsAsync<ValidationException>(() => Create(Request()));
    }
    [Fact]
    public async Task RejectsMalformedOrForeignItemsAndRefundInProgress()
    {
        var dto = Request(); dto.Items.Add(new() { OrderItemId = itemId, Quantity = 1 });
        await Assert.ThrowsAsync<ValidationException>(() => Create(dto));
        dto = Request(); dto.Items[0].OrderItemId += 100;
        await Assert.ThrowsAsync<ValidationException>(() => Create(dto));
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request(0)));
        dto = Request(); dto.RequestId = Guid.Empty; await Assert.ThrowsAsync<ValidationException>(() => Create(dto));
        using (var db = new CatalogDbContext(options)) { db.Orders.Single().RefundStatus = "Pending"; db.SaveChanges(); }
        await Assert.ThrowsAsync<ValidationException>(() => Create(Request()));
    }
    [Fact]
    public void RoutesRequireTheAppropriateRole()
    {
        foreach (var name in new[] { "Create", "Mine", "Options" })
            Assert.Contains(typeof(ReturnController).GetMethod(name)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), a => a.Roles == "Customer");
        foreach (var name in new[] { "List", "Review" })
            Assert.Contains(typeof(ReturnController).GetMethod(name)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), a => a.Roles == "Admin");
    }
    public void Dispose() => connection.Dispose();
}
