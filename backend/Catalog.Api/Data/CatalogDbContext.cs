using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Customer>().HasIndex(c => c.NormalizedEmail).IsUnique();
        model.Entity<Customer>().Property(c => c.FirstName).HasMaxLength(100);
        model.Entity<Customer>().Property(c => c.LastName).HasMaxLength(100);
        model.Entity<Customer>().Property(c => c.Email).HasMaxLength(254);
        model.Entity<Customer>().Property(c => c.NormalizedEmail).HasMaxLength(254);
        model.Entity<Customer>().Property(c => c.Phone).HasMaxLength(25);
        model.Entity<Customer>().Property(c => c.Address).HasMaxLength(500);
        model.Entity<Customer>().Property(c => c.PasswordHash).HasMaxLength(512);
        model.Entity<AdminUser>().HasIndex(x => x.Username).IsUnique();
        model.Entity<AdminUser>().Property(x => x.Username).HasMaxLength(100);
        model.Entity<AdminUser>().Property(x => x.PasswordHash).HasMaxLength(512);
        model.Entity<Category>().Property(x => x.Name).HasMaxLength(100);
        model.Entity<Category>().Property(x => x.Description).HasMaxLength(2000);
        model.Entity<Product>().Property(x => x.Name).HasMaxLength(160);
        model.Entity<Product>().Property(x => x.Description).HasMaxLength(2000);
        model.Entity<Product>().Property(x => x.ImageUrl).HasMaxLength(2048);
        model.Entity<Product>().Property(x => x.Price).HasPrecision(12, 2);
        model.Entity<Product>().HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Category>().HasData(new Category { Id = 1, Name = "General", Description = "Nuestro catálogo" });
    }
}

