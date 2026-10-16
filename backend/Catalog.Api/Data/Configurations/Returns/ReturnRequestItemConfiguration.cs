using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de ReturnRequestItem: clave compuesta, relaciones y restricciones de cantidades.
public class ReturnRequestItemConfiguration : IEntityTypeConfiguration<ReturnRequestItem>
{
    public void Configure(EntityTypeBuilder<ReturnRequestItem> b)
    {
        b.HasKey(r => new { r.ReturnRequestId, r.OrderItemId });
        b.HasOne(r => r.ReturnRequest).WithMany(r => r.Items).HasForeignKey(r => r.ReturnRequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.OrderItem).WithMany().HasForeignKey(r => r.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("ReturnRequestItems", t =>
        {
            t.HasCheckConstraint("CK_ReturnItem_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_ReturnItem_Received", "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] AND [RestockedQuantity] >= 0 AND [RestockedQuantity] <= [ReceivedQuantity]");
        });
    }
}
