using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;


namespace Catalog.Api.Services;

public class AuthService(CatalogDbContext db, IPasswordHasher<AdminUser> hasher, TokenService tokens)
{
    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto, CancellationToken ct)
    {
        var user = await db.AdminUsers.SingleOrDefaultAsync(x => x.Username == dto.Username, ct);
        if (user is null) return null;
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, dto.Password);
            await db.SaveChangesAsync(ct);
        }
        return tokens.Create(user.Id, user.Username, "Admin");
    }
}


