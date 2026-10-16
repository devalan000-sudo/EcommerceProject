using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de ReturnRefundItem: clave compuesta, relaciones y restricción de cantidad y precio.
public class ReturnRefundItemConfiguration : IEntityTypeConfiguration<ReturnRefundItem>
{
    public void Configure(EntityTypeBuilder<ReturnRefundItem> i)
    {
        i.HasKey(x => new { x.ReturnRefundId, x.OrderItemId });
        i.HasOne(x => x.ReturnRefund).WithMany(x => x.Items).HasForeignKey(x => x.ReturnRefundId).OnDelete(DeleteBehavior.Cascade);
        i.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        i.Property(x => x.UnitPrice).HasPrecision(12, 2);
        i.ToTable("ReturnRefundItems", t => t.HasCheckConstraint("CK_ReturnRefundItem_Quantity", "[Quantity] > 0 AND [UnitPrice] > 0"));
    }
}
