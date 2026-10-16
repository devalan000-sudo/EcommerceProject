using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de Category: longitudes de columnas y la categoría inicial sembrada.
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(c => c.Name).HasMaxLength(100);
        b.Property(c => c.Description).HasMaxLength(2000);
        b.HasData(new Category { Id = 1, Name = "General", Description = "Nuestro catálogo" });
    }
}
