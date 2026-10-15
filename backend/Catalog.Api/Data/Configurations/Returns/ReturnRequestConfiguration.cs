using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de ReturnRequest: índices, longitudes, estado como texto y versión concurrente.
public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
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
