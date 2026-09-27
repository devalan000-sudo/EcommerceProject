using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> b)
    {
        b.HasOne(a => a.Customer).WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(a => a.CustomerId);
        b.HasIndex(a => new { a.CustomerId, a.IsDefault }).IsUnique().HasFilter("[IsDefault] = 1");
        b.Property(a => a.Version).IsConcurrencyToken();
        b.Property(a => a.Label).HasMaxLength(40);
        b.Property(a => a.FirstName).HasMaxLength(100);
        b.Property(a => a.LastName).HasMaxLength(100);
        b.Property(a => a.Phone).HasMaxLength(25);
        b.Property(a => a.Street).HasMaxLength(100);
        b.Property(a => a.ExteriorNumber).HasMaxLength(20);
        b.Property(a => a.InteriorNumber).HasMaxLength(20);
        b.Property(a => a.Neighborhood).HasMaxLength(80);
        b.Property(a => a.City).HasMaxLength(80);
        b.Property(a => a.State).HasMaxLength(80);
        b.Property(a => a.PostalCode).HasMaxLength(12);
        b.Property(a => a.Country).HasMaxLength(60);
        b.Property(a => a.References).HasMaxLength(150);
    }
}
