using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

// Pruebas de integración de servicios con EF relacional en memoria, no con SQL Server local.
public class CatalogServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly ProductService products;
    private readonly CategoryService categories;

    public CatalogServiceTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        products = new(db); categories = new(db);
    }

    [Fact]
    public async Task ProductCrudPreservesCategoryAndVisibilityRules()
    {
        var category = await categories.CreateAsync(new(" Ropa ", "Colección"), default);
        var created = await products.CreateAsync(new ProductCreateDto {
            Name = " Playera ", Description = "Algodón", Price = 349.50m,
            ImageUrl = "https://example.com/a.png", CategoryId = category.Id
        }, default);
        Assert.Equal("Playera", created.Name);
        Assert.Equal("Ropa", created.CategoryName);
        Assert.Equal(349.50m, created.Price);
        Assert.True(await products.CategoryExistsAsync(category.Id, default));
        Assert.False(await products.CategoryExistsAsync(int.MaxValue, default));
        Assert.Contains(await products.ListAsync(false, default), p => p.Id == created.Id);

        Assert.True(await products.UpdateAsync(created.Id, new ProductUpdateDto {
            Name = " Playera nueva ", Description = "Actualizada", Price = 399,
            ImageUrl = "https://example.com/b.png", CategoryId = 1, IsActive = false
        }, default));
        Assert.Null(await products.GetAsync(created.Id, false, default));
        Assert.DoesNotContain(await products.ListAsync(false, default), p => p.Id == created.Id);
        var edited = await products.GetAsync(created.Id, true, default);
        Assert.Equal("Playera nueva", edited!.Name);
        Assert.Equal("General", edited.CategoryName);
        Assert.Equal(399m, edited.Price);
        Assert.Equal("https://example.com/b.png", edited.ImageUrl);
        Assert.Contains(await products.ListAsync(true, default), p => p.Id == created.Id);
        Assert.True(await products.DeleteAsync(created.Id, default));
        Assert.Null(await products.GetAsync(created.Id, true, default));
        Assert.False(await products.DeleteAsync(created.Id, default));
        Assert.False(await products.UpdateAsync(created.Id, new ProductUpdateDto(), default));
    }

    [Fact]
    public async Task CategoryCannotBeDeletedWhileEvenAnInactiveProductUsesIt()
    {
        var category = await categories.CreateAsync(new("Gorras", "Accesorios"), default);
        var item = new Product { Name = "Gorra", Price = 100, CategoryId = category.Id, IsActive = false };
        db.Products.Add(item); await db.SaveChangesAsync();
        Assert.Equal("in-use", await categories.DeleteAsync(category.Id, default));
        Assert.True(await categories.UpdateAsync(category.Id, new(" Gorras nuevas ", "Nueva descripción"), default));
        Assert.Contains(await categories.ListAsync(default), c => c.Id == category.Id && c.Name == "Gorras nuevas" && c.Description == "Nueva descripción");
        Assert.True(await products.DeleteAsync(item.Id, default));
        Assert.Equal("deleted", await categories.DeleteAsync(category.Id, default));
        Assert.Equal("missing", await categories.DeleteAsync(category.Id, default));
        Assert.False(await categories.UpdateAsync(category.Id, new("Otra", ""), default));
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
