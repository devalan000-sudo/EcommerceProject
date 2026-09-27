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

public class ReturnReceiptTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<CatalogDbContext> options;
    private readonly Guid id;
    private readonly int itemId;
    private readonly Guid version;
    public ReturnReceiptTests()
    {
        connection.Open(); options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options;
        using var db = new CatalogDbContext(options); db.Database.EnsureCreated();
        var product = new Product { Name = "Playera", CategoryId = 1 };
        var variant = new ProductVariant { Product = product, Stock = 10, Size = "M", Color = "Rojo" };
        db.Add(variant); db.SaveChanges();
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Number = "RECEIPT-TEST",
            Customer = new Customer { Email = "receipt@example.test", NormalizedEmail = "RECEIPT@EXAMPLE.TEST" },
            PaymentStatus = PaymentStatus.Paid,
            FulfillmentStatus = FulfillmentStatus.Delivered,
            Total = 300,
            Items = [new() { ProductId = product.Id, VariantId = variant.Id, ProductName = product.Name, Size = "M", Color = "Rojo", Quantity = 3, UnitPrice = 100, LineTotal = 300 }]
        };
        db.Add(order); db.SaveChanges(); itemId = order.Items[0].Id;
        var request = new ReturnRequest { Order = order, Status = ReturnStatus.Approved, ClientRequestId = Guid.NewGuid(), Reason = "Talla", Items = [new() { OrderItemId = itemId, Quantity = 3 }] };
        db.Add(request); db.SaveChanges(); id = request.Id; version = request.Version;
    }
    private ReturnReceiveDto Request(int received = 1, int restocked = 1, Guid? currentVersion = null) => new()
    {
        RequestId = Guid.NewGuid(),
        Version = currentVersion ?? version,
        Note = "Revisado: unidades aptas indicadas.",
        Items = [new() { OrderItemId = itemId, ReceivedQuantity = received, RestockedQuantity = restocked }]
    };
    private async Task<ReturnResponseDto> Receive(ReturnReceiveDto dto)
    { await using var db = new CatalogDbContext(options); return (await new ReturnService(db).ReceiveAsync(id, 7, dto, default))!; }

    [Fact]
    public async Task PartialThenCompleteAccumulatesAndReplayNeverRestocksTwice()
    {
        var firstDto = Request(); var first = await Receive(firstDto);
        Assert.Equal(ReturnStatus.PartiallyReceived, first.Status);
        var secondDto = Request(2, 1, first.Version); var complete = await Receive(secondDto);
        Assert.Equal(ReturnStatus.Received, complete.Status); Assert.Equal(3, complete.Items[0].ReceivedQuantity); Assert.Equal(2, complete.Items[0].RestockedQuantity);
        await Receive(firstDto); await Receive(secondDto);
        using var db = new CatalogDbContext(options);
        Assert.Equal(12, db.ProductVariants.Single().Stock); Assert.Equal(2, db.ReturnReceipts.Count());
        Assert.Equal(2, db.EmailOutbox.Count(m => m.Kind == "ReturnReceived"));
        Assert.All(db.ReturnReceipts, r => Assert.Equal(7, r.AdminId));
        Assert.Equal(PaymentStatus.Paid, db.Orders.Single().PaymentStatus); Assert.Equal(300m, db.Orders.Single().Total);
        Assert.Null(db.Orders.Single().RefundRequestId);
    }
    [Fact]
    public async Task RejectsChangedPayloadForSameKeyAndStaleVersion()
    {
        var dto = Request(); await Receive(dto); dto.Note = "Otro contenido";
        await Assert.ThrowsAsync<ValidationException>(() => Receive(dto));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Receive(Request()));
        using var db = new CatalogDbContext(options); Assert.Equal(11, db.ProductVariants.Single().Stock);
    }
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 2)]
    [InlineData(4, 1)]
    public async Task InvalidQuantitiesLeaveEverythingUntouched(int received, int restocked)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Receive(Request(received, restocked)));
        using var db = new CatalogDbContext(options); Assert.Equal(10, db.ProductVariants.Single().Stock);
        Assert.Empty(db.ReturnReceipts); Assert.Equal(0, db.ReturnRequestItems.Single().ReceivedQuantity);
        Assert.Empty(db.EmailOutbox.Where(m => m.Kind == "ReturnReceived"));
    }
    [Theory]
    [InlineData(ReturnStatus.Requested)]
    [InlineData(ReturnStatus.Rejected)]
    public async Task RequiresApproval(ReturnStatus status)
    {
        using (var db = new CatalogDbContext(options)) { db.ReturnRequests.Single().Status = status; db.SaveChanges(); }
        await Assert.ThrowsAsync<ValidationException>(() => Receive(Request()));
    }
    [Fact]
    public async Task ChangedVariantCanBeReceivedWithoutRestock()
    {
        using (var db = new CatalogDbContext(options)) { db.ProductVariants.Single().Color = "Negro"; db.SaveChanges(); }
        await Assert.ThrowsAsync<ValidationException>(() => Receive(Request()));
        var result = await Receive(Request(3, 0)); Assert.Equal(ReturnStatus.Received, result.Status);
        using var verify = new CatalogDbContext(options); Assert.Equal(10, verify.ProductVariants.Single().Stock);
    }
    [Fact]
    public async Task StockLimitAndDuplicateOrForeignLinesAreRejected()
    {
        var dto = Request(); dto.Items.Add(dto.Items[0]); await Assert.ThrowsAsync<ValidationException>(() => Receive(dto));
        dto = Request(); dto.Items[0].OrderItemId += 100; await Assert.ThrowsAsync<ValidationException>(() => Receive(dto));
        using (var db = new CatalogDbContext(options)) { db.ProductVariants.Single().Stock = 1000000; db.SaveChanges(); }
        await Assert.ThrowsAsync<ValidationException>(() => Receive(Request()));
        using var verify = new CatalogDbContext(options); Assert.Empty(verify.ReturnReceipts); Assert.Equal(0, verify.ReturnRequestItems.Single().ReceivedQuantity);
    }
    [Fact]
    public async Task ReceivedUnitsRemainClaimedAndHistoryIsVisibleOnlyToOwner()
    {
        await Receive(Request(3, 0)); using var db = new CatalogDbContext(options); var service = new ReturnService(db); var order = db.Orders.Single();
        Assert.False((await service.OptionsAsync(order.Id, order.CustomerId, default))!.CanRequest);
        var list = await service.ListAsync(order.CustomerId, order.Id, null, 1, default);
        Assert.Single(Assert.Single(list.Items).Receipts);
        Assert.Empty((await service.ListAsync(order.CustomerId + 1, order.Id, null, 1, default)).Items);
    }
    [Fact]
    public void ReceiptEndpointIsAdminOnly() => Assert.Contains(
        typeof(ReturnController).GetMethod("Receive")!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), a => a.Roles == "Admin");
    public void Dispose() => connection.Dispose();
}
