using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

public static class EmailConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var mail = model.Entity<EmailOutbox>();
        mail.HasKey(m => m.Id); mail.HasIndex(m => m.EventKey).IsUnique();
        mail.Property(m => m.EventKey).HasMaxLength(150); mail.Property(m => m.Kind).HasMaxLength(60);
        mail.Property(m => m.Status).HasMaxLength(30); mail.Property(m => m.TokenHash).HasMaxLength(64);
        mail.Property(m => m.ProviderMessageId).HasMaxLength(100);
        mail.HasIndex(m => new { m.Status, m.NextAttemptUtc });
        var token = model.Entity<CustomerEmailToken>();
        token.HasKey(t => t.Hash); token.Property(t => t.Hash).HasMaxLength(64);
        token.Property(t => t.Purpose).HasMaxLength(20); token.Property(t => t.NormalizedEmail).HasMaxLength(254);
        token.HasIndex(t => new { t.CustomerId, t.Purpose, t.CreatedAtUtc });
        token.HasOne(t => t.Customer).WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<Customer>().Property(c => c.SecurityStamp).IsConcurrencyToken();
    }
}
