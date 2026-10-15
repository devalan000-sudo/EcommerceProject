using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de ProductVariant: longitudes, índice único por talla/color y relación con Product.
public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> v)
    {
        v.Property(x => x.Size).HasMaxLength(30);
        v.Property(x => x.Color).HasMaxLength(50);
        v.Property(x => x.ImageUrl).HasMaxLength(2048);
        v.HasIndex(x => new { x.ProductId, x.Size, x.Color }).IsUnique();
        v.HasOne(x => x.Product).WithMany(p => p.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
