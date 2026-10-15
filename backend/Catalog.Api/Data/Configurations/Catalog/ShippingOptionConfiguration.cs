using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de ShippingOption: longitudes, tipo de envío como texto y precisión del precio.
public class ShippingOptionConfiguration : IEntityTypeConfiguration<ShippingOption>
{
    public void Configure(EntityTypeBuilder<ShippingOption> s)
    {
        s.Property(x => x.Name).HasMaxLength(100);
        s.Property(x => x.Description).HasMaxLength(500);
        s.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        s.Property(x => x.Price).HasPrecision(12, 2);
    }
}
