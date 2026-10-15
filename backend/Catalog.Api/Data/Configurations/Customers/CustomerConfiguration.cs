using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de Customer: longitudes, correo normalizado único y sello de seguridad concurrente.
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.HasIndex(c => c.NormalizedEmail).IsUnique();
        b.Property(c => c.FirstName).HasMaxLength(100);
        b.Property(c => c.LastName).HasMaxLength(100);
        b.Property(c => c.Email).HasMaxLength(254);
        b.Property(c => c.NormalizedEmail).HasMaxLength(254);
        b.Property(c => c.Phone).HasMaxLength(25);
        b.Property(c => c.Address).HasMaxLength(500);
        b.Property(c => c.PasswordHash).HasMaxLength(512);
        b.Property(c => c.SecurityStamp).IsConcurrencyToken();
    }
}
