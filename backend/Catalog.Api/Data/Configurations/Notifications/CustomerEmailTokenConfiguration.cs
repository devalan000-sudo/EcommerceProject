using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de CustomerEmailToken: hash como clave, longitudes y relación con Customer.
public class CustomerEmailTokenConfiguration : IEntityTypeConfiguration<CustomerEmailToken>
{
    public void Configure(EntityTypeBuilder<CustomerEmailToken> t)
    {
        t.HasKey(x => x.Hash);
        t.Property(x => x.Hash).HasMaxLength(64);
        t.Property(x => x.Purpose).HasMaxLength(20);
        t.Property(x => x.NormalizedEmail).HasMaxLength(254);
        t.HasIndex(x => new { x.CustomerId, x.Purpose, x.CreatedAtUtc });
        t.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}
