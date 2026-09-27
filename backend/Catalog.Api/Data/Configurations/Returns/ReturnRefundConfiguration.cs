using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

public static class ReturnRefundConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var r = model.Entity<ReturnRefund>();
        r.HasOne(x => x.ReturnRequest).WithMany(x => x.Refunds).HasForeignKey(x => x.ReturnRequestId).OnDelete(DeleteBehavior.Restrict);
        r.HasIndex(x => new { x.ReturnRequestId, x.ClientRequestId }).IsUnique();
        r.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        r.HasIndex(x => x.StripeRefundId).IsUnique().HasFilter("[StripeRefundId] IS NOT NULL");
        r.Property(x => x.Amount).HasPrecision(18, 2);
        r.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        r.Property(x => x.RequestHash).HasMaxLength(64);
        r.Property(x => x.Note).HasMaxLength(1000);
        r.Property(x => x.ManualReference).HasMaxLength(200);
        r.Property(x => x.PaymentIntentId).HasMaxLength(255);
        r.Property(x => x.StripeRefundId).HasMaxLength(255);
        r.Property(x => x.Version).IsConcurrencyToken();
        r.ToTable("ReturnRefunds", t => t.HasCheckConstraint("CK_ReturnRefund_Amount", "[Amount] > 0"));
        var i = model.Entity<ReturnRefundItem>();
        i.HasKey(x => new { x.ReturnRefundId, x.OrderItemId });
        i.HasOne(x => x.ReturnRefund).WithMany(x => x.Items).HasForeignKey(x => x.ReturnRefundId).OnDelete(DeleteBehavior.Cascade);
        i.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        i.Property(x => x.UnitPrice).HasPrecision(12, 2);
        i.ToTable("ReturnRefundItems", t => t.HasCheckConstraint("CK_ReturnRefundItem_Quantity", "[Quantity] > 0 AND [UnitPrice] > 0"));
    }
}
