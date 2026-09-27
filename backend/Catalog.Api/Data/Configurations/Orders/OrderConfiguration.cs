using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> order)
    {
        order.HasIndex(o => o.Number).IsUnique();
        order.Property(o => o.CancellationReason).HasMaxLength(500);
        order.Property(o => o.RefundReason).HasMaxLength(500);
        order.Property(o => o.RefundRequestHash).HasMaxLength(64);
        order.Property(o => o.RefundStatus).HasMaxLength(30);
        order.Property(o => o.StripeRefundId).HasMaxLength(255);
        order.Property(o => o.RefundPaymentIntentId).HasMaxLength(255);
        order.HasIndex(o => o.StripeRefundId).IsUnique().HasFilter("[StripeRefundId] IS NOT NULL");
        order.Property(o => o.Carrier).HasMaxLength(100);
        order.Property(o => o.TrackingNumber).HasMaxLength(100);
        order.Property(o => o.FulfillmentVersion).IsConcurrencyToken();
        order.Property(o => o.ManualPaymentRequestHash).HasMaxLength(64);
        order.Property(o => o.ManualPaymentMethod).HasMaxLength(30);
        order.Property(o => o.ManualPaymentReference).HasMaxLength(200);
        order.Property(o => o.StripeSessionId).HasMaxLength(255);
        order.Property(o => o.RequestHash).HasMaxLength(64);
        order.HasIndex(o => new { o.CustomerId, o.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
        order.HasIndex(o => o.StripeSessionId).IsUnique().HasFilter("[StripeSessionId] IS NOT NULL");
        order.HasIndex(o => new { o.CustomerId, o.CreatedAtUtc });
        order.Property(o => o.Number).HasMaxLength(40);
        order.Property(o => o.FirstName).HasMaxLength(100);
        order.Property(o => o.LastName).HasMaxLength(100);
        order.Property(o => o.Email).HasMaxLength(254);
        order.Property(o => o.Phone).HasMaxLength(25);
        order.Property(o => o.Address).HasMaxLength(500);
        order.Property(o => o.Currency).HasMaxLength(3);
        order.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        order.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20);
        order.Property(o => o.FulfillmentStatus).HasConversion<string>().HasMaxLength(30);
        order.Property(o => o.Subtotal).HasPrecision(18, 2);
        order.Property(o => o.ShippingCost).HasPrecision(18, 2);
        order.Property(o => o.Total).HasPrecision(18, 2);
        order.HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> item)
    {
        item.Property(i => i.ProductName).HasMaxLength(160);
        item.Property(i => i.ImageUrl).HasMaxLength(2048);
        item.Property(i => i.Details).HasMaxLength(200);
        item.Property(i => i.UnitPrice).HasPrecision(12, 2);
        item.Property(i => i.LineTotal).HasPrecision(18, 2);
        item.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        item.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}
