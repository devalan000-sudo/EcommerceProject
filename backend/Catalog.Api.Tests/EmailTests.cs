using System.Net;
using System.Text;
using System.Text.Json;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

public class EmailTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly IConfiguration config;
    private readonly IDataProtectionProvider protector = new EphemeralDataProtectionProvider();
    private readonly PasswordHasher<Customer> hasher = new();
    private readonly EmailSettings settings;
    private readonly CustomerEmailService email;
    private readonly FakePostmark handler = new();
    private readonly HttpClient http;
    private readonly EmailDeliveryService delivery;
    private readonly Customer user = new() { Email = "owner@example.com", NormalizedEmail = "OWNER@EXAMPLE.COM", SecurityStamp = Guid.NewGuid() };
    public EmailTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        user.PasswordHash = hasher.HashPassword(user, "original-password-123"); db.Add(user); db.SaveChanges();
        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postmark:Enabled"] = "true",
            ["Postmark:ServerToken"] = "unit-test-fixture",
            ["Postmark:From"] = "shop@example.com",
            ["Email:FrontendUrl"] = "http://localhost:5173",
            ["Jwt:Key"] = "unit-test-jwt-key-with-at-least-32-bytes",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test"
        }).Build();
        settings = new(config); email = new(db, settings, protector, hasher); http = new HttpClient(handler);
        delivery = new(db, new PostmarkGateway(http, config, settings), settings, protector);
    }
    private async Task<(string Raw, EmailOutbox Mail)> Issue(string purpose)
    {
        await email.RequestAsync(user.Email, purpose, default);
        var mail = await db.EmailOutbox.AsNoTracking().SingleAsync(m => m.Kind == purpose);
        return (protector.CreateProtector("customer-email-v1").Unprotect(mail.ProtectedToken!), mail);
    }
    [Fact]
    public async Task ConfirmationUsesHashedOneTimePurposeBoundToken()
    {
        var (raw, mail) = await Issue("confirm");
        Assert.DoesNotContain(raw, mail.ProtectedToken!); Assert.NotEqual(raw, mail.TokenHash);
        Assert.False(await email.ConsumeAsync(raw, "reset", "changed-password-123", default));
        Assert.True(await email.ConsumeAsync(raw, "confirm", null, default));
        Assert.True((await db.Customers.AsNoTracking().SingleAsync()).EmailConfirmed);
        Assert.False(await email.ConsumeAsync(raw, "confirm", null, default));
        await delivery.ProcessAsync(mail.Id, default); Assert.Equal(0, handler.Count);
    }
    [Fact]
    public async Task ResetInvalidatesOldSessionsAndAllOlderTokens()
    {
        var oldStamp = user.SecurityStamp;
        var (confirm, _) = await Issue("confirm"); var (raw, _) = await Issue("reset");
        Assert.True(await email.ConsumeAsync(raw, "reset", "new-password-456", default));
        var saved = await db.Customers.AsNoTracking().SingleAsync();
        Assert.False(CustomerSessionValidation.Matches(saved.SecurityStamp, oldStamp.ToString()));
        Assert.False(CustomerSessionValidation.Matches(saved.SecurityStamp, null));
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(saved, saved.PasswordHash, "new-password-456"));
        Assert.False(await email.ConsumeAsync(raw, "reset", "another-password-789", default));
        Assert.False(await email.ConsumeAsync(confirm, "confirm", null, default));
    }
    [Fact]
    public async Task ExpiredAndEmailChangedTokensAreRejected()
    {
        var (raw, _) = await Issue("confirm");
        await db.CustomerEmailTokens.ExecuteUpdateAsync(u => u.SetProperty(t => t.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear(); Assert.False(await email.ConsumeAsync(raw, "confirm", null, default));
        await db.CustomerEmailTokens.ExecuteUpdateAsync(u => u.SetProperty(t => t.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(10)));
        await db.Customers.ExecuteUpdateAsync(u => u.SetProperty(c => c.NormalizedEmail, "CHANGED@EXAMPLE.COM"));
        db.ChangeTracker.Clear(); Assert.False(await email.ConsumeAsync(raw, "confirm", null, default));
    }
    [Fact]
    public async Task RequestsAreThrottledAndUnknownAddressQueuesNothing()
    {
        await email.RequestAsync("unknown@example.com", "reset", default);
        Assert.Empty(await db.EmailOutbox.ToListAsync());
        await Issue("reset"); await email.RequestAsync("OWNER@example.com", "reset", default);
        Assert.Single(await db.EmailOutbox.ToListAsync());
    }
    [Fact]
    public async Task PostmarkReceivesProtectedLinkWithoutTrackingAndSentMailIsNotRepeated()
    {
        var (_, mail) = await Issue("confirm");
        db.ChangeTracker.Clear();
        await delivery.ProcessAsync(mail.Id, default); db.ChangeTracker.Clear(); await delivery.ProcessAsync(mail.Id, default);
        Assert.Equal(1, handler.Count);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("owner@example.com", body.RootElement.GetProperty("To").GetString());
        Assert.Contains("/account/security#action=confirm&token=", body.RootElement.GetProperty("TextBody").GetString());
        Assert.Equal("None", body.RootElement.GetProperty("TrackLinks").GetString());
        var saved = await db.EmailOutbox.AsNoTracking().SingleAsync(); Assert.Equal("Sent", saved.Status); Assert.Null(saved.ProtectedToken);
    }
    [Fact]
    public async Task ProviderFailureRetriesAndLeasePreventsParallelSend()
    {
        var (_, mail) = await Issue("reset"); db.ChangeTracker.Clear(); handler.Fail = true;
        await delivery.ProcessAsync(mail.Id, default);
        var saved = await db.EmailOutbox.AsNoTracking().SingleAsync(); Assert.Equal("Pending", saved.Status); Assert.Equal(1, saved.Attempts);
        handler.Fail = false;
        await db.EmailOutbox.ExecuteUpdateAsync(u => u.SetProperty(m => m.NextAttemptUtc, DateTime.UtcNow.AddSeconds(-1)).SetProperty(m => m.LeaseUntilUtc, DateTime.UtcNow.AddMinutes(1)));
        db.ChangeTracker.Clear(); await delivery.ProcessAsync(mail.Id, default); Assert.Equal(1, handler.Count);
        await db.EmailOutbox.ExecuteUpdateAsync(u => u.SetProperty(m => m.LeaseUntilUtc, (DateTime?)null));
        db.ChangeTracker.Clear(); await delivery.ProcessAsync(mail.Id, default); Assert.Equal(2, handler.Count);
        Assert.Equal("Sent", (await db.EmailOutbox.AsNoTracking().SingleAsync()).Status);
    }
    [Fact]
    public async Task DisabledConfigurationNeverCallsProvider()
    {
        var (_, mail) = await Issue("reset"); config["Postmark:Enabled"] = "false";
        await delivery.ProcessAsync(mail.Id, default); Assert.Equal(0, handler.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => email.RequestAsync(user.Email, "reset", default));
    }
    [Fact]
    public async Task OrderNotificationsAreAtomicAndOnlyTargetVerifiedOwner()
    {
        var order = new Order { Id = Guid.NewGuid(), Number = "TEST-NOTIFICATION", CustomerId = user.Id, Email = "untrusted@example.com" };
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.Orders.Add(order); await db.SaveChangesAsync(); Assert.Single(await db.EmailOutbox.ToListAsync());
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear(); Assert.Empty(await db.EmailOutbox.ToListAsync());
        order.Customer = null!;
        db.Orders.Add(order); await db.SaveChangesAsync();
        var mail = await db.EmailOutbox.SingleAsync(); db.ChangeTracker.Clear();
        await delivery.ProcessAsync(mail.Id, default); Assert.Equal(0, handler.Count);
        await db.Customers.ExecuteUpdateAsync(u => u.SetProperty(c => c.EmailConfirmed, true));
        await db.EmailOutbox.ExecuteUpdateAsync(u => u.SetProperty(m => m.NextAttemptUtc, DateTime.UtcNow.AddSeconds(-1)));
        db.ChangeTracker.Clear(); await delivery.ProcessAsync(mail.Id, default);
        using var body = JsonDocument.Parse(handler.Body!); Assert.Equal("owner@example.com", body.RootElement.GetProperty("To").GetString());
        var savedOrder = await db.Orders.SingleAsync(); savedOrder.PaymentStatus = PaymentStatus.Paid; await db.SaveChangesAsync(); await db.SaveChangesAsync();
        Assert.Single(await db.EmailOutbox.Where(m => m.Kind == "PaymentPaid").ToListAsync());
    }
    [Fact]
    public async Task RegistrationQueuesConfirmationAndEmailChangeRequiresPassword()
    {
        var customers = new CustomerService(db, hasher, new TokenService(config), email);
        await customers.RegisterAsync(new() { FirstName = "Ana", LastName = "López", Email = "new@example.com", Address = "Calle Uno 123", Password = "new-user-password-123" }, default);
        var added = await db.Customers.SingleAsync(c => c.Email == "new@example.com");
        Assert.Equal(added.Id, (await db.CustomerEmailTokens.SingleAsync()).CustomerId);
        var update = new CustomerUpdateDto { FirstName = "Ana", LastName = "López", Email = "changed@example.com", Address = "Calle Uno 123" };
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => customers.UpdateAsync(added.Id, update, default));
        var stamp = added.SecurityStamp; update.CurrentPassword = "new-user-password-123";
        var result = await customers.UpdateAsync(added.Id, update, default);
        Assert.False(result!.EmailConfirmed); Assert.NotEqual(stamp, added.SecurityStamp);
    }
    [Fact]
    public void EmailConfigurationRejectsRecipientListsAndUnsafeReturnUrls()
    {
        Assert.False(EmailSettings.ValidAddress("a@example.com,b@example.com"));
        config["Email:FrontendUrl"] = "http://example.com"; Assert.False(settings.Ready);
        config["Email:FrontendUrl"] = "https://example.com"; Assert.True(settings.Ready);
        Assert.True(CustomerSessionValidation.Matches(Guid.Empty, null));
    }
    [Theory]
    [InlineData("ReturnApproved", "Tu devolución fue aprobada")]
    [InlineData("ReturnRejected", "Respuesta a tu solicitud de devolución")]
    [InlineData("ReturnReceived", "Recibimos artículos de tu devolución")]
    [InlineData("ReturnRefundSucceeded", "Reembolso de tu devolución confirmado")]
    public async Task AfterSalesMailTargetsVerifiedOwnerAndIsNotRepeated(string kind, string subject)
    {
        user.EmailConfirmed = true;
        var order = new Order { Id = Guid.NewGuid(), Number = "TEST-AFTERSALES", CustomerId = user.Id, Email = "untrusted@example.com" };
        db.Orders.Add(order); await db.SaveChangesAsync();
        var eventId = Guid.NewGuid();
        AfterSalesEmails.Queue(db, order, kind, eventId); AfterSalesEmails.Queue(db, order, kind, eventId);
        await db.SaveChangesAsync();
        var mail = await db.EmailOutbox.SingleAsync(m => m.Kind == kind);
        db.ChangeTracker.Clear(); await delivery.ProcessAsync(mail.Id, default);
        db.ChangeTracker.Clear(); await delivery.ProcessAsync(mail.Id, default);
        Assert.Equal(1, handler.Count);
        using var message = JsonDocument.Parse(handler.Body!);
        Assert.Equal(subject, message.RootElement.GetProperty("Subject").GetString());
        Assert.Equal("owner@example.com", message.RootElement.GetProperty("To").GetString());
        Assert.Contains($"/account?tab=orders&order={order.Id}", message.RootElement.GetProperty("TextBody").GetString());
        Assert.DoesNotContain("untrusted@example.com", handler.Body!);
    }
    [Fact]
    public async Task AfterSalesRollbackDoesNotLeaveNotificationAndUnverifiedOwnerWaits()
    {
        var order = new Order { Id = Guid.NewGuid(), Number = "AFTERSALES-ROLLBACK", CustomerId = user.Id };
        db.Orders.Add(order); await db.SaveChangesAsync();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            AfterSalesEmails.Queue(db, order, "ReturnReceived", Guid.NewGuid()); await db.SaveChangesAsync(); await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear(); Assert.Empty(await db.EmailOutbox.Where(m => m.Kind == "ReturnReceived").ToListAsync());
        AfterSalesEmails.Queue(db, order, "ReturnApproved", Guid.NewGuid()); await db.SaveChangesAsync();
        var mail = await db.EmailOutbox.SingleAsync(m => m.Kind == "ReturnApproved");
        db.ChangeTracker.Clear(); await delivery.ProcessAsync(mail.Id, default);
        Assert.Equal(0, handler.Count); Assert.Equal("Pending", (await db.EmailOutbox.AsNoTracking().SingleAsync(m => m.Id == mail.Id)).Status);
    }
    public void Dispose() { http.Dispose(); db.Dispose(); connection.Dispose(); }
    private class FakePostmark : HttpMessageHandler
    {
        public int Count; public bool Fail; public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++; Body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Equal("https://api.postmarkapp.com/email", request.RequestUri!.ToString());
            Assert.True(request.Headers.Contains("X-Postmark-Server-Token"));
            return Fail ? new(HttpStatusCode.ServiceUnavailable) : new(HttpStatusCode.OK) { Content = new StringContent("{\"ErrorCode\":0,\"MessageID\":\"unit-test-message\"}", Encoding.UTF8, "application/json") };
        }
    }
}
