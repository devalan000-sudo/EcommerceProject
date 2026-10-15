using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de ReturnRefund: índices, precisión del monto, estado y versión concurrente.
public class ReturnRefundConfiguration : IEntityTypeConfiguration<ReturnRefund>
{
    public void Configure(EntityTypeBuilder<ReturnRefund> r)
    {
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
    }
}
