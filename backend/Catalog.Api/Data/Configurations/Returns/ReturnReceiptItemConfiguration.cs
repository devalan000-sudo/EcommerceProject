using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de ReturnReceiptItem: clave compuesta, relaciones y restricción de cantidades.
public class ReturnReceiptItemConfiguration : IEntityTypeConfiguration<ReturnReceiptItem>
{
    public void Configure(EntityTypeBuilder<ReturnReceiptItem> item)
    {
        item.HasKey(i => new { i.ReturnReceiptId, i.OrderItemId });
        item.HasOne(i => i.ReturnReceipt).WithMany(r => r.Items).HasForeignKey(i => i.ReturnReceiptId).OnDelete(DeleteBehavior.Cascade);
        item.HasOne<OrderItem>().WithMany().HasForeignKey(i => i.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        item.ToTable("ReturnReceiptItems", t => t.HasCheckConstraint("CK_ReceiptItem_Quantities", "[ReceivedQuantity] > 0 AND [RestockedQuantity] >= 0 AND [RestockedQuantity] <= [ReceivedQuantity]"));
    }
}
