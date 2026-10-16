using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de Product: longitudes, precisión del precio y clave foránea a Category.
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(p => p.Name).HasMaxLength(160);
        b.Property(p => p.Description).HasMaxLength(2000);
        b.Property(p => p.ImageUrl).HasMaxLength(2048);
        b.Property(p => p.Price).HasPrecision(12, 2);
        b.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
