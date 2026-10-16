using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Catalog.Api.Data;
using Catalog.Api.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class CustomerEmailService(CatalogDbContext db, EmailSettings settings, IDataProtectionProvider protection, IPasswordHasher<Customer> hasher)
{
    public bool Ready => settings.Ready;
    // Genera un hash no reversible para buscar tokens sin guardar el valor original.
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    // Añade un token protegido para confirmación o recuperación de cuenta.
    public void AddToken(Customer user, string purpose)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(raw); var expiry = DateTime.UtcNow.AddMinutes(purpose == "confirm" ? 1440 : 30);
        db.CustomerEmailTokens.Add(new() { Hash = hash, Customer = user, Purpose = purpose, NormalizedEmail = user.NormalizedEmail, SecurityStamp = user.SecurityStamp, ExpiresAtUtc = expiry });
        db.EmailOutbox.Add(new()
        {
            EventKey = $"token:{hash}",
            CustomerId = user.Id,
            Kind = purpose,
            TokenHash = hash,
            ProtectedToken = protection.CreateProtector("customer-email-v1").Protect(raw),
            ExpiresAtUtc = expiry
        });
    }
    // Envía o prepara un token para una solicitud de correo del cliente.
    public async Task RequestAsync(string email, string purpose, CancellationToken ct)
    {
        if (!Ready) throw new InvalidOperationException("El servicio de correo aún no está configurado.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var normalized = CustomerService.NormalizeEmail(email);
        var user = await db.Customers.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized, ct);
        if (user is null || (purpose == "confirm" && user.EmailConfirmed)) return;
        var since = DateTime.UtcNow.AddMinutes(-1);
        if (await db.CustomerEmailTokens.AnyAsync(t => t.CustomerId == user.Id && t.Purpose == purpose && t.CreatedAtUtc > since, ct)) return;
        AddToken(user, purpose); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    // Consume un token de un solo uso y aplica la acción solicitada.
    public async Task<bool> ConsumeAsync(string raw, string purpose, string? password, CancellationToken ct)
    {
        if (raw.Length != 64) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var hash = Hash(raw);
        var token = await FindValidTokenAsync(hash, purpose, ct);
        if (token is null) return false;
        ApplyTokenAction(token, purpose, password);
        token.UsedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }

    private async Task<CustomerEmailToken?> FindValidTokenAsync(string hash, string purpose, CancellationToken ct)
    {
        var token = await db.CustomerEmailTokens.Include(t => t.Customer)
            .SingleOrDefaultAsync(t => t.Hash == hash, ct);
        if (token is null || token.Purpose != purpose || token.UsedAtUtc.HasValue ||
            token.ExpiresAtUtc <= DateTime.UtcNow || token.SecurityStamp != token.Customer.SecurityStamp ||
            token.NormalizedEmail != token.Customer.NormalizedEmail)
            return null;
        return token;
    }

    private void ApplyTokenAction(CustomerEmailToken token, string purpose, string? password)
    {
        if (purpose == "confirm")
        {
            token.Customer.EmailConfirmed = true;
            return;
        }

        if (password is null || password.Length < 12 || password.Length > 256)
            throw new ValidationException("La contraseña debe tener entre 12 y 256 caracteres.");
        token.Customer.PasswordHash = hasher.HashPassword(token.Customer, password);
        token.Customer.SecurityStamp = Guid.NewGuid();
    }
}
