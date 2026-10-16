using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de OrderItem: longitudes, precisión y relaciones con Order y Product.
public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> item)
    {
        item.Property(i => i.ProductName).HasMaxLength(160);
        item.Property(i => i.ImageUrl).HasMaxLength(2048);
        item.Property(i => i.Size).HasMaxLength(30);
        item.Property(i => i.Color).HasMaxLength(50);
        item.Property(i => i.Details).HasMaxLength(200);
        item.Property(i => i.UnitPrice).HasPrecision(12, 2);
        item.Property(i => i.LineTotal).HasPrecision(18, 2);
        item.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        item.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}
