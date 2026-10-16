using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Data;

// Configuración Fluent API de ReturnReceipt: índice único por solicitud y longitudes del hash y la nota.
public class ReturnReceiptConfiguration : IEntityTypeConfiguration<ReturnReceipt>
{
    public void Configure(EntityTypeBuilder<ReturnReceipt> receipt)
    {
        receipt.HasOne(r => r.ReturnRequest).WithMany(r => r.Receipts).HasForeignKey(r => r.ReturnRequestId).OnDelete(DeleteBehavior.Restrict);
        receipt.HasIndex(r => new { r.ReturnRequestId, r.ClientRequestId }).IsUnique();
        receipt.Property(r => r.RequestHash).HasMaxLength(64);
        receipt.Property(r => r.Note).HasMaxLength(1000);
    }
}
