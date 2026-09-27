using System.ComponentModel.DataAnnotations;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class InventoryTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly InventoryService inventory;
    private readonly OrderService orders;
    private readonly Customer customer = new() { Email = "inventory@example.com", NormalizedEmail = "INVENTORY@EXAMPLE.COM" };
    private readonly Product product = new() { Name = "Playera", Price = 100, CategoryId = 1 };
    private readonly ShippingOption shipping = new() { Name = "Entrega local", Kind = ShippingKind.FlatRate, Price = 50, IsActive = true };
    public InventoryTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated(); db.AddRange(customer, product, shipping); db.SaveChanges();
        inventory = new(db); orders = new(db);
    }
    private async Task<VariantResponseDto> Variant(int stock = 2, string color = "negro") => await inventory.SaveVariantAsync(product.Id, null,
        new VariantWriteDto { Size = "m", Color = color, Stock = stock }, default);
    private OrderCreateDto Request(int variant, int quantity = 1, PaymentMethod method = PaymentMethod.Card) => new()
    {
        FirstName = "Ana",
        LastName = "López",
        Email = "inventory@example.com",
        Address = "Calle Uno 123, Centro",
        ShippingAddress = new() { Street = "Calle Uno", ExteriorNumber = "123", Neighborhood = "Centro", City = "Ciudad", State = "Estado", PostalCode = "01234" },
        PaymentMethod = method,
        ShippingOptionId = shipping.Id,
        Items = [new() { ProductId = product.Id, VariantId = variant, Quantity = quantity }]
    };

    [Fact]
    public async Task CatalogAggregatePreservesActiveExpiredHeldAndReleasedReservationRules()
    {
        var variant = await Variant(5);
        var order = await orders.CreateAsync(customer.Id, Request(variant.Id, 2), default);
        await inventory.ReserveForPaymentAsync(order.Id, customer.Id, default);
        var catalog = new ProductService(db);
        async Task<int> Available() => (await catalog.GetAsync(product.Id, false, default))!.Variants!.Single().AvailableStock;
        Assert.Equal(3, await Available());
        await db.InventoryReservations.ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(5, await Available());
        await db.Orders.Where(o => o.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.StripeHold, true));
        Assert.Equal(3, await Available());
        await db.InventoryReservations.ExecuteUpdateAsync(s => s.SetProperty(r => r.Released, true));
        Assert.Equal(5, await Available());
        await db.ProductVariants.ExecuteUpdateAsync(s => s.SetProperty(v => v.IsActive, false));
        Assert.Empty((await catalog.GetAsync(product.Id, false, default))!.Variants!);
        Assert.Single((await catalog.GetAsync(product.Id, true, default))!.Variants!);
    }

    [Fact]
    public async Task ReservationLastsFifteenMinutesAndRetriesDoNotExtendIt()
    {
        var variant = await Variant(); var order = await orders.CreateAsync(customer.Id, Request(variant.Id), default);
        Assert.Empty(await db.InventoryReservations.ToListAsync());
        Assert.Equal(150m, order.Total);
        var before = DateTime.UtcNow;
        var expires = await inventory.ReserveForPaymentAsync(order.Id, customer.Id, default);
        Assert.InRange(expires, before.AddMinutes(15), DateTime.UtcNow.AddMinutes(15));
        Assert.Equal(expires, await inventory.ReserveForPaymentAsync(order.Id, customer.Id, default));
        Assert.Single(await db.InventoryReservations.ToListAsync());
        Assert.Equal(1, (await InventoryService.Query(db, product.Id, false, DateTime.UtcNow).SingleAsync()).AvailableStock);
        await db.InventoryReservations.ExecuteUpdateAsync(u => u.SetProperty(r => r.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear();
        Assert.Equal(2, (await InventoryService.Query(db, product.Id, false, DateTime.UtcNow).SingleAsync()).AvailableStock);
        await Assert.ThrowsAsync<ValidationException>(() => inventory.ReserveForPaymentAsync(order.Id, customer.Id, default));
    }

    [Fact]
    public async Task CompetingOrdersCannotReserveTheSameLastUnit()
    {
        var variant = await Variant(1);
        var first = await orders.CreateAsync(customer.Id, Request(variant.Id), default);
        var second = await orders.CreateAsync(customer.Id, Request(variant.Id), default);
        await inventory.ReserveForPaymentAsync(first.Id, customer.Id, default);
        await Assert.ThrowsAsync<ValidationException>(() => inventory.ReserveForPaymentAsync(second.Id, customer.Id, default));
        Assert.Single(await db.InventoryReservations.AsNoTracking().ToListAsync());
        await inventory.ReleaseAsync(first.Id, default); db.ChangeTracker.Clear();
        await inventory.ReserveForPaymentAsync(second.Id, customer.Id, default);
        Assert.Equal(1, await db.InventoryReservations.CountAsync(r => !r.Released));
    }

    [Fact]
    public async Task WhatsAppAndOtherCustomerCannotReserve()
    {
        var variant = await Variant();
        var order = await orders.CreateAsync(customer.Id, Request(variant.Id, method: PaymentMethod.WhatsApp), default);
        await Assert.ThrowsAsync<ValidationException>(() => inventory.ReserveForPaymentAsync(order.Id, customer.Id, default));
        var card = await orders.CreateAsync(customer.Id, Request(variant.Id), default);
        await Assert.ThrowsAsync<ValidationException>(() => inventory.ReserveForPaymentAsync(card.Id, customer.Id + 1, default));
        Assert.Empty(await db.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task VariantEditsRespectReservationsAndNormalizedUniqueness()
    {
        var variant = await Variant();
        Assert.Equal("M", variant.Size); Assert.Equal("NEGRO", variant.Color);
        await Assert.ThrowsAsync<ValidationException>(() => Variant(3, " NEGRO "));
        var order = await orders.CreateAsync(customer.Id, Request(variant.Id), default);
        await inventory.ReserveForPaymentAsync(order.Id, customer.Id, default);
        await Assert.ThrowsAsync<ValidationException>(() => inventory.SaveVariantAsync(product.Id, variant.Id, new() { Size = "M", Color = "NEGRO", Stock = 0 }, default));
        await Assert.ThrowsAsync<ValidationException>(() => inventory.SaveVariantAsync(product.Id, variant.Id, new() { Size = "M", Color = "NEGRO", Stock = 2, IsActive = false }, default));
    }

    [Fact]
    public async Task EachColorHasOwnStockAndOrderSnapshot()
    {
        var black = await Variant(2); var red = await Variant(1, "rojo");
        var request = Request(black.Id); request.Items.Add(new() { ProductId = product.Id, VariantId = red.Id, Quantity = 1 });
        var order = await orders.CreateAsync(customer.Id, request, default);
        Assert.Equal(250m, order.Total); Assert.Equal(2, order.Items.Count);
        Assert.Contains(order.Items, i => i.Color == "ROJO");
        await Assert.ThrowsAsync<ValidationException>(() => orders.CreateAsync(customer.Id, Request(red.Id, 2), default));
        shipping.Price = 90; await db.SaveChangesAsync();
        Assert.Equal(50m, (await orders.GetAsync(order.Id, customer.Id, default))!.ShippingCost);
    }

    [Fact]
    public async Task FailedMultiItemReservationRollsBackEverything()
    {
        var black = await Variant(2); var red = await Variant(1, "rojo");
        var request = Request(black.Id); request.Items.Add(new() { ProductId = product.Id, VariantId = red.Id, Quantity = 1 });
        var order = await orders.CreateAsync(customer.Id, request, default);
        await inventory.SaveVariantAsync(product.Id, red.Id, new() { Size = "M", Color = "ROJO", Stock = 0 }, default);
        await Assert.ThrowsAsync<ValidationException>(() => inventory.ReserveForPaymentAsync(order.Id, customer.Id, default));
        Assert.Empty(await db.InventoryReservations.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(ShippingKind.Free)]
    [InlineData(ShippingKind.Pickup)]
    public async Task FreeAndPickupCannotChargeShipping(ShippingKind kind)
    {
        var service = new ShippingService(db);
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(null, new() { Name = "Opción", Kind = kind, Price = 10 }, default));
        var option = await service.SaveAsync(null, new() { Name = "Opción", Kind = kind, Price = 0, IsActive = false }, default);
        Assert.DoesNotContain(await service.ListAsync(false, default), s => s.Id == option!.Id);
        Assert.Contains(await service.ListAsync(true, default), s => s.Id == option!.Id);
    }
    [Theory]
    [InlineData("complete", "Paid", 1)]
    [InlineData("expired", "Failed", 2)]
    public async Task StripeTerminalEventsAreIdempotent(string status, string expected, int stock)
    {
        var variant = await Variant();
        var order = await orders.CreateAsync(customer.Id, Request(variant.Id), default);
        await inventory.ReserveForPaymentAsync(order.Id, customer.Id, default, true);
        await db.InventoryReservations.ExecuteUpdateAsync(u => u.SetProperty(r => r.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear();
        Assert.Equal(1, (await InventoryService.Query(db, product.Id, false, DateTime.UtcNow).SingleAsync()).AvailableStock);
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var payments = new PaymentService(db, inventory, new StripeGateway(new HttpClient(), config));
        var session = System.Text.Json.JsonSerializer.SerializeToElement(new { id = "cs_test_fixture", livemode = false, mode = "payment", client_reference_id = order.Id.ToString(), metadata = new { order_id = order.Id.ToString() }, currency = "mxn", amount_total = 15000, status, payment_status = status == "complete" ? "paid" : "unpaid" });
        await payments.ApplySessionAsync(order.Id, session, default);
        await payments.ApplySessionAsync(order.Id, session, default);
        Assert.Equal(expected, (await orders.GetAsync(order.Id, customer.Id, default))!.PaymentStatus.ToString());
        Assert.Equal(stock, (await db.ProductVariants.SingleAsync()).Stock);
        Assert.False((await db.Orders.SingleAsync()).StripeHold);
        Assert.All(await db.InventoryReservations.ToListAsync(), r => Assert.True(r.Released));
    }

    [Fact]
    public async Task StripeAmountMismatchCannotConfirmPayment()
    {
        var variant = await Variant(); var order = await orders.CreateAsync(customer.Id, Request(variant.Id), default);
        await inventory.ReserveForPaymentAsync(order.Id, customer.Id, default, true);
        var payments = new PaymentService(db, inventory, new StripeGateway(new HttpClient(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));
        var session = System.Text.Json.JsonSerializer.SerializeToElement(new { id = "cs_test_fixture", livemode = false, mode = "payment", client_reference_id = order.Id.ToString(), metadata = new { order_id = order.Id.ToString() }, currency = "mxn", amount_total = 1, status = "complete", payment_status = "paid" });
        await Assert.ThrowsAsync<ValidationException>(() => payments.ApplySessionAsync(order.Id, session, default));
        Assert.Equal(PaymentStatus.Pending, (await orders.GetAsync(order.Id, customer.Id, default))!.PaymentStatus);
        Assert.Equal(2, (await db.ProductVariants.SingleAsync()).Stock);
    }

    [Fact]
    public async Task RequestRetryReturnsSameOrderButRejectsChangedPayload()
    {
        var variant = await Variant(); var request = Request(variant.Id); request.ClientRequestId = Guid.NewGuid();
        var first = await orders.CreateAsync(customer.Id, request, default);
        Assert.Equal(first.Id, (await orders.CreateAsync(customer.Id, request, default)).Id);
        request.Address = "Otra dirección 1234";
        await Assert.ThrowsAsync<ValidationException>(() => orders.CreateAsync(customer.Id, request, default));
        Assert.Single(await db.Orders.ToListAsync());
    }
    private ManualPaymentDto Manual(OrderResponseDto order) => new() { RequestId = Guid.NewGuid(), Version = order.FulfillmentVersion, Method = "Transfer", ShippingName = order.ShippingName!, ShippingCost = order.ShippingCost, AmountReceived = order.Total };

    [Fact]
    public async Task ManualPaymentDeductsExactlyOnceAndKeepsAudit()
    {
        var variant = await Variant(3);
        var order = await orders.CreateAsync(customer.Id, Request(variant.Id, 2, PaymentMethod.WhatsApp), default);
        var service = new ManualPaymentService(db, orders); var dto = Manual(order);
        var paid = await service.ConfirmAsync(order.Id, 7, dto, default);
        Assert.Equal(PaymentStatus.Paid, paid!.PaymentStatus);
        await service.ConfirmAsync(order.Id, 7, dto, default);
        Assert.Equal(1, (await db.ProductVariants.SingleAsync()).Stock);
        Assert.Equal(7, (await db.Orders.SingleAsync()).ManualPaymentAdminId);
        Assert.NotNull(paid.ManualPaymentConfirmedAtUtc);
        dto.AmountReceived = 1;
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(order.Id, 7, dto, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(order.Id, 7, Manual(order), default));
    }

    [Fact]
    public async Task ManualPaymentRespectsStripeHoldsAndRollsBackAllItems()
    {
        var black = await Variant(2); var red = await Variant(1, "rojo");
        var request = Request(black.Id, method: PaymentMethod.WhatsApp);
        request.Items.Add(new() { ProductId = product.Id, VariantId = red.Id, Quantity = 1 });
        var order = await orders.CreateAsync(customer.Id, request, default);
        var card = await orders.CreateAsync(customer.Id, Request(red.Id), default);
        await inventory.ReserveForPaymentAsync(card.Id, customer.Id, default, true);
        await db.InventoryReservations.ExecuteUpdateAsync(u => u.SetProperty(r => r.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ValidationException>(() => new ManualPaymentService(db, orders).ConfirmAsync(order.Id, 7, Manual(order), default));
        Assert.Equal(2, (await db.ProductVariants.SingleAsync(v => v.Id == black.Id)).Stock);
        Assert.Equal(1, (await db.ProductVariants.SingleAsync(v => v.Id == red.Id)).Stock);
        Assert.Equal(PaymentStatus.Pending, (await orders.GetAsync(order.Id, customer.Id, default))!.PaymentStatus);
    }

    [Fact]
    public async Task ManualPaymentQuotesUnknownDeliveryAndRejectsWrongAmounts()
    {
        var variant = await Variant(); var request = Request(variant.Id, method: PaymentMethod.WhatsApp); request.ShippingOptionId = null;
        var order = await orders.CreateAsync(customer.Id, request, default);
        var dto = Manual(order); dto.ShippingName = "Entrega acordada"; dto.ShippingCost = 25; dto.AmountReceived = 124;
        var service = new ManualPaymentService(db, orders);
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(order.Id, 7, dto, default));
        dto.AmountReceived = 125;
        var result = await service.ConfirmAsync(order.Id, 7, dto, default);
        Assert.Equal(125m, result!.Total); Assert.Equal("Entrega acordada", result.ShippingName);
    }

    [Fact]
    public async Task ManualPaymentRejectsCardLegacyChangedShippingAndStaleVersion()
    {
        var variant = await Variant(); var service = new ManualPaymentService(db, orders);
        var card = await orders.CreateAsync(customer.Id, Request(variant.Id), default);
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(card.Id, 7, Manual(card), default));
        var order = await orders.CreateAsync(customer.Id, Request(variant.Id, method: PaymentMethod.WhatsApp), default);
        var dto = Manual(order); dto.ShippingCost = 0; dto.AmountReceived = 100;
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(order.Id, 7, dto, default));
        dto = Manual(order); dto.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.ConfirmAsync(order.Id, 7, dto, default));
        var item = await db.OrderItems.SingleAsync(i => i.OrderId == order.Id); item.VariantId = null; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(order.Id, 7, Manual(order), default));
        Assert.Equal(2, (await db.ProductVariants.SingleAsync()).Stock);
    }

    [Fact]
    public void ManualConfirmationRequiresAdminAuthorization()
    {
        var auth = Assert.Single(typeof(Catalog.Api.Controllers.ManualPaymentController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal("Admin", auth.Roles);
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
