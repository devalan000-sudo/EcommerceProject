using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de EmailOutbox: clave, evento único, estados y cola de reintentos.
public class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> m)
    {
        m.HasKey(x => x.Id);
        m.HasIndex(x => x.EventKey).IsUnique();
        m.Property(x => x.EventKey).HasMaxLength(150);
        m.Property(x => x.Kind).HasMaxLength(60);
        m.Property(x => x.Status).HasMaxLength(30);
        m.Property(x => x.TokenHash).HasMaxLength(64);
        m.Property(x => x.ProviderMessageId).HasMaxLength(100);
        m.HasIndex(x => new { x.Status, x.NextAttemptUtc });
    }
}
