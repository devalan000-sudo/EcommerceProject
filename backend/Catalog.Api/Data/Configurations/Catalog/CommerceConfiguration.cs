using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

public static class CommerceConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var variant = model.Entity<ProductVariant>();
        variant.Property(v => v.Size).HasMaxLength(30);
        variant.Property(v => v.ImageUrl).HasMaxLength(2048);
        variant.Property(v => v.Color).HasMaxLength(50);
        variant.HasIndex(v => new { v.ProductId, v.Size, v.Color }).IsUnique();
        variant.HasOne(v => v.Product).WithMany(p => p.Variants).HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
        var reservation = model.Entity<InventoryReservation>();
        reservation.HasIndex(r => new { r.OrderId, r.VariantId }).IsUnique();
        reservation.HasIndex(r => new { r.VariantId, r.ExpiresAtUtc });
        reservation.HasOne(r => r.Variant).WithMany(v => v.Reservations).HasForeignKey(r => r.VariantId).OnDelete(DeleteBehavior.Restrict);
        reservation.HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Cascade);
        var shipping = model.Entity<ShippingOption>();
        shipping.Property(s => s.Name).HasMaxLength(100);
        shipping.Property(s => s.Description).HasMaxLength(500);
        shipping.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
        shipping.Property(s => s.Price).HasPrecision(12, 2);
        model.Entity<Order>().Property(o => o.ShippingName).HasMaxLength(100);
        model.Entity<OrderItem>().Property(i => i.Size).HasMaxLength(30);
        model.Entity<OrderItem>().Property(i => i.Color).HasMaxLength(50);
        // Variante y envío del pedido son referencias históricas escalares, no FKs.
    }
}
