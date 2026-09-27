using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

public static class ReturnReceiptConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var receipt = model.Entity<ReturnReceipt>();
        receipt.HasOne(r => r.ReturnRequest).WithMany(r => r.Receipts).HasForeignKey(r => r.ReturnRequestId).OnDelete(DeleteBehavior.Restrict);
        receipt.HasIndex(r => new { r.ReturnRequestId, r.ClientRequestId }).IsUnique();
        receipt.Property(r => r.RequestHash).HasMaxLength(64);
        receipt.Property(r => r.Note).HasMaxLength(1000);
        var item = model.Entity<ReturnReceiptItem>();
        item.HasKey(i => new { i.ReturnReceiptId, i.OrderItemId });
        item.HasOne(i => i.ReturnReceipt).WithMany(r => r.Items).HasForeignKey(i => i.ReturnReceiptId).OnDelete(DeleteBehavior.Cascade);
        item.HasOne<OrderItem>().WithMany().HasForeignKey(i => i.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        item.ToTable("ReturnReceiptItems", t => t.HasCheckConstraint("CK_ReceiptItem_Quantities", "[ReceivedQuantity] > 0 AND [RestockedQuantity] >= 0 AND [RestockedQuantity] <= [ReceivedQuantity]"));
        model.Entity<ReturnRequestItem>().ToTable("ReturnRequestItems", t => t.HasCheckConstraint("CK_ReturnItem_Received", "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] AND [RestockedQuantity] >= 0 AND [RestockedQuantity] <= [ReceivedQuantity]"));
    }
}
