using Catalog.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

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

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfiguration(new ReturnConfiguration());
        ReturnReceiptConfiguration.Configure(model);
        ReturnRefundConfiguration.Configure(model);
        model.ApplyConfiguration(new ReturnItemConfiguration());
        model.ApplyConfiguration(new CustomerAddressConfiguration());
        model.ApplyConfiguration(new StoreSettingsConfiguration());
        model.ApplyConfiguration(new OrderConfiguration());
        EmailConfiguration.Configure(model);
        CommerceConfiguration.Configure(model);
        model.ApplyConfiguration(new OrderItemConfiguration());
        model.Entity<Customer>().HasIndex(c => c.NormalizedEmail).IsUnique();
        model.Entity<Customer>().Property(c => c.FirstName).HasMaxLength(100);
        model.Entity<Customer>().Property(c => c.LastName).HasMaxLength(100);
        model.Entity<Customer>().Property(c => c.Email).HasMaxLength(254);
        model.Entity<Customer>().Property(c => c.NormalizedEmail).HasMaxLength(254);
        model.Entity<Customer>().Property(c => c.Phone).HasMaxLength(25);
        model.Entity<Customer>().Property(c => c.Address).HasMaxLength(500);
        model.Entity<Customer>().Property(c => c.PasswordHash).HasMaxLength(512);
        model.Entity<AdminUser>().HasIndex(x => x.Username).IsUnique();
        model.Entity<AdminUser>().Property(x => x.Username).HasMaxLength(100);
        model.Entity<AdminUser>().Property(x => x.PasswordHash).HasMaxLength(512);
        model.Entity<Category>().Property(x => x.Name).HasMaxLength(100);
        model.Entity<Category>().Property(x => x.Description).HasMaxLength(2000);
        model.Entity<Product>().Property(x => x.Name).HasMaxLength(160);
        model.Entity<Product>().Property(x => x.Description).HasMaxLength(2000);
        model.Entity<Product>().Property(x => x.ImageUrl).HasMaxLength(2048);
        model.Entity<Product>().Property(x => x.Price).HasPrecision(12, 2);
        model.Entity<Product>().HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Category>().HasData(new Category { Id = 1, Name = "General", Description = "Nuestro catálogo" });
    }

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

