using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Xunit;

public class VariantTemplateTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly Product product = new() { Name = "Playera azul", CategoryId = 1, Price = 100, ImageUrl = "https://example.test/azul.jpg" };
    public VariantTemplateTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated(); db.Products.Add(product); db.SaveChanges();
    }
    [Fact]
    public async Task TemplateIsIdempotentPreservesAliasesStockAndInactiveVariants()
    {
        var old = new ProductVariant { ProductId = product.Id, Size = "CHICA", Color = "AZUL", Stock = 7, IsActive = false };
        db.ProductVariants.Add(old); await db.SaveChangesAsync();
        var templates = new VariantTemplateService(db);
        Assert.Equal(11, await templates.ApplyAsync(product.Id, new() { MainColor = "Azul", InitialStock = 10 }, default));
        Assert.Equal(0, await templates.ApplyAsync(product.Id, new() { MainColor = "Azul", InitialStock = 999 }, default));
        Assert.Equal(12, await db.ProductVariants.CountAsync()); Assert.Equal(7, old.Stock); Assert.False(old.IsActive);
        Assert.Equal(4, await db.ProductVariants.Select(v => v.Color).Distinct().CountAsync());
        Assert.All(await db.ProductVariants.Where(v => v.Color != "AZUL").ToListAsync(), v => Assert.Null(v.ImageUrl));
        Assert.All(await db.ProductVariants.Where(v => v.Color == "AZUL" && v.Id != old.Id).ToListAsync(), v => Assert.Equal(product.ImageUrl, v.ImageUrl));
    }
    [Fact]
    public async Task ColorPhotoIsSharedAcrossSizesAndOrderKeepsOriginalSnapshot()
    {
        await new VariantTemplateService(db).ApplyAsync(product.Id, new() { MainColor = "Azul", InitialStock = 10 }, default);
        var inventory = new InventoryService(db);
        var blue = await db.ProductVariants.FirstAsync(v => v.Color == "AZUL");
        await inventory.SaveVariantAsync(product.Id, blue.Id, new() { Size = blue.Size, Color = blue.Color, Stock = 10, ImageUrl = "https://example.test/blue.jpg" }, default);
        Assert.All(await db.ProductVariants.Where(v => v.Color == "AZUL").ToListAsync(), v => Assert.Equal("https://example.test/blue.jpg", v.ImageUrl));
        var customer = new Customer { Email = "template@example.test", NormalizedEmail = "TEMPLATE@EXAMPLE.TEST" };
        db.Customers.Add(customer); await db.SaveChangesAsync();
        var order = await new OrderService(db).CreateAsync(customer.Id, new()
        {
            FirstName = "Ana",
            LastName = "Prueba",
            Email = customer.Email,
            PaymentMethod = PaymentMethod.Card,
            ShippingAddress = new() { Street = "Uno", ExteriorNumber = "1", Neighborhood = "Centro", PostalCode = "01234", City = "Ciudad", State = "Estado" },
            Items = [new() { ProductId = product.Id, VariantId = blue.Id, Quantity = 1 }]
        }, default);
        Assert.Equal("https://example.test/blue.jpg", order.Items[0].ImageUrl);
        await inventory.SaveVariantAsync(product.Id, blue.Id, new() { Size = blue.Size, Color = blue.Color, Stock = 10, ImageUrl = "https://example.test/new.jpg" }, default);
        Assert.Equal("https://example.test/blue.jpg", (await db.OrderItems.SingleAsync()).ImageUrl);
        await Assert.ThrowsAsync<ValidationException>(() => inventory.SaveVariantAsync(product.Id, blue.Id, new() { Size = "XXL", Color = blue.Color, Stock = 10 }, default));
    }
    [Fact]
    public async Task RejectsAliasDuplicateAndInvalidPhoto()
    {
        var inventory = new InventoryService(db);
        await inventory.SaveVariantAsync(product.Id, null, new() { Size = "Chica", Color = "Azul", Stock = 1 }, default);
        await Assert.ThrowsAsync<ValidationException>(() => inventory.SaveVariantAsync(product.Id, null, new() { Size = "S", Color = "AZUL", Stock = 1 }, default));
        await Assert.ThrowsAsync<ValidationException>(() => inventory.SaveVariantAsync(product.Id, null, new() { Size = "L", Color = "AZUL", Stock = 1, ImageUrl = "javascript:alert(1)" }, default));
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
