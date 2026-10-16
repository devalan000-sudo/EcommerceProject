using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Catalog.Api.Controllers;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public class OrderTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly OrderService service;
    private readonly Customer customer = new() { Email = "test@example.com", NormalizedEmail = "TEST@EXAMPLE.COM" };
    private readonly Product product = new() { Name = "Playera", Price = 349.50m, ImageUrl = "https://example.com/a.png", CategoryId = 1 };
    public OrderTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        db.Customers.Add(customer); db.Products.Add(product); db.SaveChanges();
        service = new(db);
    }
    private OrderCreateDto Request() => new()
    {
        FirstName = " Ana ",
        LastName = " López ",
        Email = "ana@example.com",
        Address = "Calle Uno 123, Centro",
        ShippingAddress = new() { Street = "Calle Uno", ExteriorNumber = "123", Neighborhood = "Centro", City = "Ciudad", State = "Estado", PostalCode = "01234" },
        PaymentMethod = PaymentMethod.WhatsApp,
        Items = [new() { ProductId = product.Id, Quantity = 2, Details = "roja y negra" }]
    };

    [Fact]
    public async Task CardCheckoutWithoutShippingOptionChargesProductsAndPreservesAddress()
    {
        var variant = new ProductVariant { ProductId = product.Id, Size = "M", Color = "Azul", Stock = 5, IsActive = true };
        db.ProductVariants.Add(variant); await db.SaveChangesAsync();
        var dto = Request(); dto.PaymentMethod = PaymentMethod.Card; dto.Items[0].VariantId = variant.Id;
        var quote = await service.QuoteAsync(customer.Id, dto, default);
        var order = await service.CreateAsync(customer.Id, dto, default);
        Assert.Null(order.ShippingOptionId);
        Assert.Equal(0m, order.ShippingCost);
        Assert.Equal(order.Subtotal, order.Total);
        Assert.Equal(quote.Total, order.Total);
        Assert.Equal(dto.ShippingAddress!.FormatAddress(), order.Address);
        Assert.Equal("Gestionado por la tienda", order.ShippingName);
        dto.ShippingOptionId = int.MaxValue;
        await Assert.ThrowsAsync<ValidationException>(() => service.QuoteAsync(customer.Id, dto, default));
    }

    [Fact]
    public async Task CheckoutRejectsMissingStructuredAddressAndIncompleteFields()
    {
        var dto = Request(); dto.ShippingAddress = null;
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(customer.Id, dto, default));
        dto = Request(); dto.ShippingAddress!.Neighborhood = "   ";
        await Assert.ThrowsAsync<ValidationException>(() => service.QuoteAsync(customer.Id, dto, default));
        dto = Request(); dto.ShippingAddress!.PostalCode = "12A34";
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(customer.Id, dto, default));
        Assert.Empty(await db.Orders.ToListAsync());
    }

    [Fact]
    public async Task ListPreservesResolutionStateForPaymentResumeDecisions()
    {
        var created = await service.CreateAsync(customer.Id, Request(), default);
        var order = await db.Orders.SingleAsync(o => o.Id == created.Id);
        order.CancellationPending = true;
        await db.SaveChangesAsync();
        var summary = Assert.Single(await service.ListAsync(customer.Id, 1, default));
        Assert.True(summary.CancellationPending);
        Assert.Null(summary.RefundStatus);
        order.CancellationPending = false;
        order.RefundStatus = "Pending";
        await db.SaveChangesAsync();
        summary = Assert.Single(await service.ListAsync(customer.Id, 1, default));
        Assert.False(summary.CancellationPending);
        Assert.Equal("Pending", summary.RefundStatus);
    }

    [Fact]
    public async Task QuoteUsesServerPricesWithoutPersistingOrders()
    {
        var quote = await service.QuoteAsync(customer.Id, Request(), default);
        Assert.Equal(699m, quote.Subtotal);
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Empty(await db.OrderItems.ToListAsync());
        Assert.Empty(await db.InventoryReservations.ToListAsync());
    }

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.WhatsApp)]
    public async Task CreatesPendingOrderWithServerPricesAndIndependentSnapshots(PaymentMethod method)
    {
        var request = Request(); request.PaymentMethod = method;
        if (method == PaymentMethod.Card)
        {
            var variant = new ProductVariant { ProductId = product.Id, Size = "M", Color = "NEGRO", Stock = 10 };
            var shipping = new ShippingOption { Name = "Recogida", Kind = ShippingKind.Pickup, IsActive = true };
            db.ProductVariants.Add(variant); db.ShippingOptions.Add(shipping); await db.SaveChangesAsync();
            request.Items[0].VariantId = variant.Id; request.ShippingOptionId = shipping.Id;
        }
        var order = await service.CreateAsync(customer.Id, request, default);
        Assert.Equal(699m, order.Subtotal);
        if (method == PaymentMethod.Card) { Assert.Equal(699m, order.Total); Assert.Equal(0m, order.ShippingCost); }
        else { Assert.Null(order.Total); Assert.Null(order.ShippingCost); }
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Equal(FulfillmentStatus.PendingConfirmation, order.FulfillmentStatus);
        Assert.Equal("MXN", order.Currency);
        Assert.Equal("Ana", order.FirstName);
        product.Price = 999; product.Name = "Nuevo nombre";
        customer.Address = "Otra dirección";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await service.GetAsync(order.Id, customer.Id, default);
        Assert.Equal(349.50m, saved!.Items.Single().UnitPrice);
        Assert.Equal("Playera", saved.Items.Single().ProductName);
        Assert.Equal("roja y negra", saved.Items.Single().Details);
        Assert.Equal(request.ShippingAddress!.FormatAddress(), saved.Address);
    }

    [Fact]
    public async Task DeletingProductPreservesHistoricalOrder()
    {
        var order = await service.CreateAsync(customer.Id, Request(), default);
        db.ChangeTracker.Clear();
        Assert.True(await new ProductService(db).DeleteAsync(product.Id, default));
        var saved = await service.GetAsync(order.Id, customer.Id, default);
        Assert.Null(saved!.Items.Single().ProductId);
        Assert.Equal("Playera", saved.Items.Single().ProductName);
        Assert.Equal(699m, saved.Subtotal);
    }

    [Fact]
    public async Task CustomerCannotReadAnotherCustomersOrder()
    {
        var order = await service.CreateAsync(customer.Id, Request(), default);
        Assert.Null(await service.GetAsync(order.Id, customer.Id + 1, default));
        Assert.Empty(await service.ListAsync(customer.Id + 1, 1, default));
        Assert.Single(await service.ListAsync(customer.Id, 1, default));
        Assert.NotNull(await service.GetAsync(order.Id, null, default));
        Assert.Empty(await service.ListAsync(customer.Id, 2, default));
    }

    [Fact]
    public async Task ControllerScopesReadsToTokenOwnerAndAllowsAdminRead()
    {
        var order = await service.CreateAsync(customer.Id, Request(), default);
        var controller = new OrderController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        void SignIn(int id, string role) => controller.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)], "test"));
        SignIn(customer.Id + 1, "Customer");
        Assert.IsType<NotFoundResult>((await controller.Get(order.Id, default)).Result);
        SignIn(customer.Id, "Customer");
        Assert.IsType<OkObjectResult>((await controller.Get(order.Id, default)).Result);
        SignIn(999, "Admin");
        Assert.IsType<OkObjectResult>((await controller.Get(order.Id, default)).Result);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.IsType<UnauthorizedResult>((await controller.Get(order.Id, default)).Result);
    }

    [Fact]
    public async Task RejectsInactiveAndMissingProductsWithoutPartialOrders()
    {
        var request = Request();
        request.Items.Add(new() { ProductId = int.MaxValue, Quantity = 1 });
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(customer.Id, request, default));
        product.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(customer.Id, Request(), default));
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Empty(await db.OrderItems.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task RejectsInvalidQuantities(int quantity)
    {
        var request = Request(); request.Items[0].Quantity = quantity;
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(customer.Id, request, default));
        Assert.Empty(await db.Orders.ToListAsync());
    }

    [Fact]
    public async Task RejectsEmptyDuplicateAndMalformedOrders()
    {
        var empty = Request(); empty.Items.Clear();
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(customer.Id, empty, default));
        var duplicate = Request(); duplicate.Items.Add(duplicate.Items[0]);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(customer.Id, duplicate, default));
        var invalid = Request(); invalid.PaymentMethod = (PaymentMethod)999;
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(customer.Id, invalid, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(int.MaxValue, Request(), default));
        Assert.Empty(await db.Orders.ToListAsync());
    }
    [Fact]
    public async Task AdminCanAdvanceWhatsAppDeliveryWithoutChangingPaymentOrStock()
    {
        var original = await service.CreateAsync(customer.Id, Request(), default);
        var admin = new AdminOrderService(db, service);
        var prepared = await admin.UpdateAsync(original.Id, new() { Status = FulfillmentStatus.Preparing, Version = original.FulfillmentVersion }, default);
        var shipped = await admin.UpdateAsync(original.Id, new() { Status = FulfillmentStatus.Shipped, Version = prepared!.FulfillmentVersion, Carrier = " Paquetería ", TrackingNumber = " ABC123 " }, default);
        var delivered = await admin.UpdateAsync(original.Id, new() { Status = FulfillmentStatus.Delivered, Version = shipped!.FulfillmentVersion, Carrier = shipped.Carrier, TrackingNumber = shipped.TrackingNumber }, default);
        Assert.Equal(FulfillmentStatus.Delivered, delivered!.FulfillmentStatus);
        Assert.Equal(PaymentStatus.Pending, delivered.PaymentStatus);
        Assert.Equal(original.Subtotal, delivered.Subtotal);
        Assert.Equal("Paquetería", delivered.Carrier); Assert.Equal("ABC123", delivered.TrackingNumber);
        Assert.Empty(await db.InventoryReservations.ToListAsync());
        await Assert.ThrowsAsync<ValidationException>(() => admin.UpdateAsync(original.Id, new() { Status = FulfillmentStatus.Preparing, Version = delivered.FulfillmentVersion }, default));
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task AdminCannotPrepareCardWithoutConfirmedPayment(PaymentStatus payment)
    {
        var original = await service.CreateAsync(customer.Id, Request(), default);
        var entity = await db.Orders.SingleAsync(); entity.PaymentMethod = PaymentMethod.Card; entity.PaymentStatus = payment;
        await db.SaveChangesAsync();
        var admin = new AdminOrderService(db, service);
        await Assert.ThrowsAsync<ValidationException>(() => admin.UpdateAsync(original.Id, new() { Status = FulfillmentStatus.Preparing, Version = original.FulfillmentVersion }, default));
        Assert.Equal(FulfillmentStatus.PendingConfirmation, (await service.GetAsync(original.Id, null, default))!.FulfillmentStatus);
    }

    [Fact]
    public async Task PaidOrderAllowsLocalDeliveryAndRejectsStaleEdits()
    {
        var original = await service.CreateAsync(customer.Id, Request(), default);
        var entity = await db.Orders.SingleAsync(); entity.PaymentMethod = PaymentMethod.Card; entity.PaymentStatus = PaymentStatus.Paid;
        await db.SaveChangesAsync();
        var admin = new AdminOrderService(db, service);
        var request = new FulfillmentUpdateDto { Status = FulfillmentStatus.Preparing, Version = original.FulfillmentVersion };
        var prepared = await admin.UpdateAsync(original.Id, request, default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => admin.UpdateAsync(original.Id, request, default));
        var delivered = await admin.UpdateAsync(original.Id, new() { Status = FulfillmentStatus.Delivered, Version = prepared!.FulfillmentVersion }, default);
        Assert.Equal(FulfillmentStatus.Delivered, delivered!.FulfillmentStatus);
        Assert.Equal(PaymentStatus.Paid, delivered.PaymentStatus);
    }

    [Fact]
    public async Task RejectsSkippedStatesPartialTrackingAndMissingVersion()
    {
        var order = await service.CreateAsync(customer.Id, Request(), default);
        var admin = new AdminOrderService(db, service);
        foreach (var status in new[] { FulfillmentStatus.Shipped, FulfillmentStatus.Delivered, FulfillmentStatus.Cancelled, (FulfillmentStatus)999 })
            await Assert.ThrowsAsync<ValidationException>(() => admin.UpdateAsync(order.Id, new() { Status = status, Version = order.FulfillmentVersion }, default));
        await Assert.ThrowsAsync<ValidationException>(() => admin.UpdateAsync(order.Id, new() { Status = FulfillmentStatus.Preparing }, default));
        await Assert.ThrowsAsync<ValidationException>(() => admin.UpdateAsync(order.Id, new() { Status = FulfillmentStatus.Preparing, Version = order.FulfillmentVersion, Carrier = "Solo uno" }, default));
        Assert.Null(await admin.UpdateAsync(Guid.NewGuid(), new() { Status = FulfillmentStatus.Preparing, Version = Guid.Empty }, default));
    }

    [Fact]
    public async Task AdminListFiltersDatesSearchAndPages()
    {
        for (var i = 0; i < 21; i++) await service.CreateAsync(customer.Id, Request(), default);
        var entities = await db.Orders.OrderBy(o => o.Id).ToListAsync();
        foreach (var entity in entities) entity.CreatedAtUtc = new DateTime(2026, 9, 1, 23, 59, 59, DateTimeKind.Utc);
        entities[0].CreatedAtUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var admin = new AdminOrderService(db, service);
        var first = await admin.ListAsync(new(), default); var second = await admin.ListAsync(new() { Page = 2 }, default);
        Assert.Equal(21, first.TotalCount); Assert.Equal(20, first.Items.Count); Assert.Single(second.Items);
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
        var filtered = await admin.ListAsync(new() { From = new(2026, 9, 1), To = new(2026, 9, 1), Search = "ana@example.com", PaymentMethod = PaymentMethod.WhatsApp, PaymentStatus = PaymentStatus.Pending, FulfillmentStatus = FulfillmentStatus.PendingConfirmation }, default);
        Assert.Equal(20, filtered.TotalCount);
        Assert.Empty((await admin.ListAsync(new() { PaymentMethod = PaymentMethod.Card }, default)).Items);
        Assert.Single((await admin.ListAsync(new() { Search = entities[0].Number }, default)).Items);
        await Assert.ThrowsAsync<ValidationException>(() => admin.ListAsync(new() { From = new(2026, 9, 2), To = new(2026, 9, 1) }, default));
        await Assert.ThrowsAsync<ValidationException>(() => admin.ListAsync(new() { Page = 0 }, default));
    }

    [Fact]
    public void AdminRoutesRequireAdminRole()
    {
        var authorization = Assert.Single(typeof(AdminOrderController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal("Admin", authorization.Roles);
        Assert.DoesNotContain(typeof(AdminOrderController).GetMethods(), m => m.IsDefined(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true));
    }
    [Fact]
    public async Task CustomerHistoryKeepsOwnerScopeAcrossPagesAndReturnsDeliveryTracking()
    {
        var other = new Customer { Email = "other@example.com", NormalizedEmail = "OTHER@EXAMPLE.COM" };
        db.Customers.Add(other); await db.SaveChangesAsync();
        var foreign = await service.CreateAsync(other.Id, Request(), default);
        for (var i = 0; i < 21; i++) await service.CreateAsync(customer.Id, Request(), default);
        var controller = new OrderController(service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()), new Claim(ClaimTypes.Role, "Customer")], "test"));
        var first = (await controller.List(1, default)).Value!;
        var second = (await controller.List(2, default)).Value!;
        Assert.Equal(20, first.Count); Assert.Single(second);
        Assert.DoesNotContain(first.Concat(second), o => o.Id == foreign.Id);
        Assert.Equal(21, first.Concat(second).Select(o => o.Id).Distinct().Count());
        var own = first[0];
        var admin = new AdminOrderService(db, service);
        var prepared = await admin.UpdateAsync(own.Id, new() { Status = FulfillmentStatus.Preparing, Version = Guid.Empty }, default);
        await admin.UpdateAsync(own.Id, new() { Status = FulfillmentStatus.Shipped, Version = prepared!.FulfillmentVersion, Carrier = "Entrega de prueba", TrackingNumber = "GUIA-123" }, default);
        var detail = Assert.IsType<OrderResponseDto>(Assert.IsType<OkObjectResult>((await controller.Get(own.Id, default)).Result).Value);
        Assert.Equal("GUIA-123", detail.TrackingNumber); Assert.Equal("Entrega de prueba", detail.Carrier);
        Assert.Equal(FulfillmentStatus.Shipped, detail.FulfillmentStatus);
        Assert.Equal("Playera", detail.Items[0].ProductName);
        Assert.IsType<NotFoundResult>((await controller.Get(foreign.Id, default)).Result);
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
