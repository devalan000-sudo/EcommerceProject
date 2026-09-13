using Catalog.Api.Data;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ProductViewTests
{
    [Fact]
    public async Task ViewsAccumulateOnlyForActiveExistingProducts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var category = new Category { Name = "Ropa" };
        var active = new Product { Name = "Playera", Category = category, Price = 100 };
        var inactive = new Product { Name = "Oculto", Category = category, Price = 100, IsActive = false };
        db.Products.AddRange(active, inactive);
        await db.SaveChangesAsync();
        var service = new ProductService(db);
        Assert.True(await service.RegisterViewAsync(active.Id, default));
        Assert.True(await service.RegisterViewAsync(active.Id, default));
        Assert.False(await service.RegisterViewAsync(inactive.Id, default));
        Assert.False(await service.RegisterViewAsync(int.MaxValue, default));
        Assert.Equal(2L, (await service.GetAsync(active.Id, false, default))!.ViewCount);
        Assert.Equal(0L, (await service.GetAsync(inactive.Id, true, default))!.ViewCount);
    }
}
