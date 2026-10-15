using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de InventoryReservation: índices y relaciones con la variante y el pedido.
public class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> r)
    {
        r.HasIndex(x => new { x.OrderId, x.VariantId }).IsUnique();
        r.HasIndex(x => new { x.VariantId, x.ExpiresAtUtc });
        r.HasOne(x => x.Variant).WithMany(v => v.Reservations).HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
