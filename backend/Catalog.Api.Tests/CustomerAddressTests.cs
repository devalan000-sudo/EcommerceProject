using System.ComponentModel.DataAnnotations;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class CustomerAddressTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<CatalogDbContext> options;
    private readonly int ownerId;
    private readonly int otherId;
    public CustomerAddressTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options;
        using var db = new CatalogDbContext(options);
        db.Database.EnsureCreated();
        var owner = new Customer { Email = "owner@example.test", NormalizedEmail = "OWNER@EXAMPLE.TEST", Address = "Dirección anterior del perfil" };
        var other = new Customer { Email = "other@example.test", NormalizedEmail = "OTHER@EXAMPLE.TEST" };
        db.Customers.AddRange(owner, other); db.SaveChanges(); ownerId = owner.Id; otherId = other.Id;
    }
    private static CustomerAddressWriteDto Request(string label = "Casa", bool preferred = false) => new()
    {
        Label = label,
        FirstName = "Ana",
        LastName = "Prueba",
        Street = "Calle Uno",
        ExteriorNumber = "123",
        Neighborhood = "Centro",
        City = "Ciudad",
        State = "Estado",
        PostalCode = "01234",
        Country = "México",
        IsDefault = preferred
    };
    private async Task<CustomerAddressResponseDto> Save(CustomerAddressWriteDto dto, int? id = null)
    {
        await using var db = new CatalogDbContext(options);
        return (await new CustomerAddressService(db).SaveAsync(ownerId, id, dto, default))!;
    }
    private async Task<List<CustomerAddressResponseDto>> List(int? owner = null)
    {
        await using var db = new CatalogDbContext(options);
        return await new CustomerAddressService(db).ListAsync(owner ?? ownerId, default);
    }
    [Fact]
    public async Task ValidatesMexicanPostalCodeAndPreservesLeadingZeroAndAlphanumericNumber()
    {
        var dto = Request(); dto.PostalCode = "1234";
        await Assert.ThrowsAsync<ValidationException>(() => Save(dto));
        dto.PostalCode = "01234"; dto.ExteriorNumber = "12B";
        var saved = await Save(dto);
        Assert.Equal("01234", saved.PostalCode); Assert.Equal("12B", saved.ExteriorNumber);
        dto = Request("Sin número"); dto.ExteriorNumber = "S/N";
        Assert.Equal("S/N", (await Save(dto)).ExteriorNumber);
    }

    [Fact]
    public async Task FirstIsDefaultAndSwitchingKeepsOneDefault()
    {
        var first = await Save(Request()); Assert.True(first.IsDefault);
        var second = await Save(Request("Trabajo")); Assert.False(second.IsDefault);
        var update = Request("Trabajo", true); update.Version = second.Version;
        await Save(update, second.Id);
        var rows = await List(); Assert.Equal(second.Id, Assert.Single(rows, a => a.IsDefault).Id);
        Assert.NotEqual(first.Version, rows.Single(a => a.Id == first.Id).Version);
    }
    [Fact]
    public async Task OtherCustomerCannotListEditOrDeleteAddresses()
    {
        var saved = await Save(Request()); Assert.Empty(await List(otherId));
        await using var db = new CatalogDbContext(options); var service = new CustomerAddressService(db);
        var update = Request(); update.Version = saved.Version;
        Assert.Null(await service.SaveAsync(otherId, saved.Id, update, default));
        Assert.False(await service.DeleteAsync(otherId, saved.Id, saved.Version, default));
        Assert.Single(await List());
    }
    [Fact]
    public async Task DeleteDefaultPromotesAnotherAndLastDeleteLeavesEmpty()
    {
        var first = await Save(Request()); var second = await Save(Request("Trabajo"));
        await using (var db = new CatalogDbContext(options))
            Assert.True(await new CustomerAddressService(db).DeleteAsync(ownerId, first.Id, first.Version, default));
        var remaining = Assert.Single(await List()); Assert.Equal(second.Id, remaining.Id); Assert.True(remaining.IsDefault);
        await using (var db = new CatalogDbContext(options))
            Assert.True(await new CustomerAddressService(db).DeleteAsync(ownerId, remaining.Id, remaining.Version, default));
        Assert.Empty(await List());
    }
    [Fact]
    public async Task StaleEditsAndDeletesAreRejected()
    {
        var first = await Save(Request()); var update = Request("Trabajo"); update.Version = first.Version;
        await Save(update, first.Id);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Save(update, first.Id));
        await using var db = new CatalogDbContext(options);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new CustomerAddressService(db).DeleteAsync(ownerId, first.Id, first.Version, default));
    }
    [Fact]
    public async Task LimitOfTenStillAllowsEditing()
    {
        for (var i = 0; i < 10; i++) await Save(Request($"Casa {i}"));
        await Assert.ThrowsAsync<ValidationException>(() => Save(Request()));
        var first = (await List())[0]; var update = Request("Actualizada"); update.Version = first.Version;
        await Save(update, first.Id); Assert.Equal(10, (await List()).Count);
    }
    [Fact]
    public async Task EditingAndDeletingAddressDoesNotChangeProfileOrOrderSnapshot()
    {
        var address = await Save(Request());
        Guid orderId;
        await using (var db = new CatalogDbContext(options))
        {
            var product = new Product { Name = "Playera", CategoryId = 1, Price = 100, ImageUrl = "https://example.test/image.jpg" };
            db.Products.Add(product); await db.SaveChangesAsync();
            var order = await new OrderService(db).CreateAsync(ownerId, new()
            {
                FirstName = address.FirstName,
                LastName = address.LastName,
                Email = "owner@example.test",
                Address = address.FormattedAddress,
                ShippingAddress = address,
                PaymentMethod = PaymentMethod.WhatsApp,
                Items = [new() { ProductId = product.Id, Quantity = 1 }]
            }, default); orderId = order.Id;
        }
        var update = Request(); update.Street = "Calle modificada"; update.Version = address.Version;
        var edited = await Save(update, address.Id);
        await using (var db = new CatalogDbContext(options))
            await new CustomerAddressService(db).DeleteAsync(ownerId, edited.Id, edited.Version, default);
        await using (var db = new CatalogDbContext(options))
        {
            Assert.Equal(address.FormattedAddress, (await db.Orders.SingleAsync(o => o.Id == orderId)).Address);
            Assert.Equal("Dirección anterior del perfil", (await db.Customers.FindAsync(ownerId))!.Address);
        }
    }
    [Fact]
    public async Task RejectsAddressTooLongForOrderSnapshot()
    {
        var dto = Request(); dto.Street = new('a', 100); dto.Neighborhood = new('a', 80); dto.City = new('a', 80);
        dto.State = new('a', 80); dto.Country = new('a', 60); dto.References = new('a', 150);
        await Assert.ThrowsAsync<ValidationException>(() => Save(dto)); Assert.Empty(await List());
    }
    public void Dispose() => connection.Dispose();
}
