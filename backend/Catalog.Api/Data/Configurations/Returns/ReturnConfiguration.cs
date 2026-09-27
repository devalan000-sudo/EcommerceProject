using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

public class ReturnConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> b)
    {
        b.HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.OrderId, r.ClientRequestId }).IsUnique();
        b.HasIndex(r => new { r.Status, r.CreatedAtUtc });
        b.Property(r => r.RequestHash).HasMaxLength(64);
        b.Property(r => r.Reason).HasMaxLength(1000);
        b.Property(r => r.DecisionNote).HasMaxLength(1000);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Version).IsConcurrencyToken();
    }
}

public class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnRequestItem>
{
    public void Configure(EntityTypeBuilder<ReturnRequestItem> b)
    {
        b.HasKey(r => new { r.ReturnRequestId, r.OrderItemId });
        b.HasOne(r => r.ReturnRequest).WithMany(r => r.Items).HasForeignKey(r => r.ReturnRequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.OrderItem).WithMany().HasForeignKey(r => r.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("ReturnRequestItems", t => t.HasCheckConstraint("CK_ReturnItem_Quantity", "[Quantity] > 0"));
    }
}
