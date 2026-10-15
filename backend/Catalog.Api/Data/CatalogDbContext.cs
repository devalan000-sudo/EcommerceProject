using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

// Contexto EF Core: expone un DbSet por cada tabla y aplica el mapeo.
// SaveChanges encola automáticamente correos de pedidos creados o modificados.
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnReceipt> ReturnReceipts => Set<ReturnReceipt>();
    public DbSet<ReturnRefund> ReturnRefunds => Set<ReturnRefund>();
    public DbSet<ReturnRequestItem> ReturnRequestItems => Set<ReturnRequestItem>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();
    public DbSet<CustomerEmailToken> CustomerEmailTokens => Set<CustomerEmailToken>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<ShippingOption> ShippingOptions => Set<ShippingOption>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();

    // El mapeo de cada tabla vive en las clases IEntityTypeConfiguration de Data/Configurations.
    // ApplyConfigurationsFromAssembly las descubre y aplica automáticamente en este ensamblado.
    protected override void OnModelCreating(ModelBuilder model)
        => model.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

    private void QueueOrderEmails()
    {
        foreach (var entry in ChangeTracker.Entries<Order>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
        {
            var order = entry.Entity;
            var kinds = new List<string>();
            if (entry.State == EntityState.Added) kinds.Add("OrderCreated");
            else
            {
                if (entry.Property(o => o.PaymentStatus).IsModified) kinds.Add($"Payment{order.PaymentStatus}");
                if (entry.Property(o => o.FulfillmentStatus).IsModified) kinds.Add($"Delivery{order.FulfillmentStatus}");
            }
            foreach (var kind in kinds)
            {
                var key = $"order:{order.Id}:{kind}";
                if (!EmailOutbox.Local.Any(m => m.EventKey == key))
                    EmailOutbox.Add(new() { EventKey = key, OrderId = order.Id, CustomerId = order.CustomerId, Kind = kind });
            }
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { QueueOrderEmails(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { QueueOrderEmails(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}

