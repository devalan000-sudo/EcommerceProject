using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
namespace Catalog.Api.Services;

public class CustomerService(CatalogDbContext db, IPasswordHasher<Customer> hasher, TokenService tokens)
{
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static CustomerResponseDto Response(Customer user) => new(user.Id, user.FirstName, user.LastName, user.Phone, user.Email, user.Address);
    private static void Apply(Customer user, CustomerProfileDto dto)
    {
        user.FirstName = dto.FirstName.Trim(); user.LastName = dto.LastName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
        user.Email = dto.Email.Trim(); user.NormalizedEmail = NormalizeEmail(dto.Email);
        user.Address = dto.Address.Trim();
    }
    private async Task EnsureEmailAvailable(string email, int? exceptId, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        if (await db.Customers.AnyAsync(c => c.NormalizedEmail == normalized && c.Id != exceptId, ct))
            throw new DuplicateEmailException();
    }
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new DuplicateEmailException(); }
    }
    public async Task<LoginResponseDto> RegisterAsync(CustomerRegisterDto dto, CancellationToken ct)
    {
        await EnsureEmailAvailable(dto.Email, null, ct);
        var user = new Customer(); Apply(user, dto);
        user.PasswordHash = hasher.HashPassword(user, dto.Password);
        db.Customers.Add(user); await Save(ct);
        return tokens.Create(user.Id, user.Email, "Customer");
    }
    public async Task<LoginResponseDto?> LoginAsync(CustomerLoginDto dto, CancellationToken ct)
    {
        var normalized = NormalizeEmail(dto.Email);
        var user = await db.Customers.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized, ct);
        if (user is null) return null;
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        { user.PasswordHash = hasher.HashPassword(user, dto.Password); await db.SaveChangesAsync(ct); }
        return tokens.Create(user.Id, user.Email, "Customer");
    }
    public async Task<CustomerResponseDto?> GetAsync(int id, CancellationToken ct)
    {
        var user = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        return user is null ? null : Response(user);
    }
    public async Task<CustomerResponseDto?> UpdateAsync(int id, CustomerProfileDto dto, CancellationToken ct)
    {
        var user = await db.Customers.FindAsync([id], ct);
        if (user is null) return null;
        await EnsureEmailAvailable(dto.Email, id, ct);
        Apply(user, dto); await Save(ct);
        return Response(user);
    }
}
