using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
namespace Catalog.Api.Services;

public class CustomerService(CatalogDbContext db, IPasswordHasher<Customer> hasher, TokenService tokens, CustomerEmailService? emails = null)
{
    // Normaliza el correo para que la unicidad no dependa de mayúsculas o espacios.
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    // Convierte una entidad de cliente en la respuesta pública sin exponer su contraseña.
    private static CustomerResponseDto Response(Customer user) => new(user.Id, user.FirstName, user.LastName, user.Phone, user.Email, user.Address, user.EmailConfirmed);
    // Copia y limpia los datos editables del perfil.
    private static void Apply(Customer user, CustomerProfileDto dto)
    {
        user.FirstName = dto.FirstName.Trim(); user.LastName = dto.LastName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
        user.Email = dto.Email.Trim(); user.NormalizedEmail = NormalizeEmail(dto.Email);
        if (dto.Address is not null) user.Address = dto.Address.Trim();
    }
    // Comprueba la unicidad del correo antes de registrar o editar una cuenta.
    private async Task EnsureEmailAvailable(string email, int? exceptId, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        if (await db.Customers.AnyAsync(c => c.NormalizedEmail == normalized && c.Id != exceptId, ct))
            throw new DuplicateEmailException();
    }
    // Guarda cambios y traduce una carrera de índice único a un error de dominio.
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new DuplicateEmailException(); }
    }
    // Registra un cliente, almacena su contraseña con hash y prepara la confirmación de correo.
    public async Task<RegistrationResponseDto> RegisterAsync(CustomerRegisterDto dto, CancellationToken ct)
    {
        if (emails is not null && !emails.Ready)
            throw new InvalidOperationException("El registro requiere que el servicio de correo esté configurado para enviar el enlace de confirmación.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EnsureEmailAvailable(dto.Email, null, ct);
        var user = new Customer { SecurityStamp = Guid.NewGuid() }; Apply(user, dto);
        user.PasswordHash = hasher.HashPassword(user, dto.Password);
        db.Customers.Add(user); await Save(ct);
        if (emails?.Ready == true) { emails.AddToken(user, "confirm"); await Save(ct); }
        await tx.CommitAsync(ct);
        return new(user.Email, true, "Cuenta creada. Revisa tu correo y confirma la dirección antes de iniciar sesión.");
    }
    // Valida el acceso del cliente y exige que su correo esté confirmado.
    public async Task<LoginResponseDto?> LoginAsync(CustomerLoginDto dto, CancellationToken ct)
    {
        var normalized = NormalizeEmail(dto.Email);
        var user = await db.Customers.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized, ct);
        if (user is null) return null;
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed) return null;
        if (!user.EmailConfirmed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        { user.PasswordHash = hasher.HashPassword(user, dto.Password); await db.SaveChangesAsync(ct); }
        return tokens.Create(user.Id, user.Email, "Customer", user.SecurityStamp);
    }
    // Obtiene el perfil de un cliente sin devolver datos sensibles.
    public async Task<CustomerResponseDto?> GetAsync(int id, CancellationToken ct)
    {
        var user = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        return user is null ? null : Response(user);
    }
    // Actualiza el perfil y revoca la sesión si el cliente cambia su correo.
    public async Task<CustomerResponseDto?> UpdateAsync(int id, CustomerProfileDto dto, CancellationToken ct)
    {
        var user = await db.Customers.FindAsync([id], ct);
        if (user is null) return null;
        await EnsureEmailAvailable(dto.Email, id, ct);
        if (user.NormalizedEmail != NormalizeEmail(dto.Email))
        {
            if (dto is not CustomerUpdateDto update || string.IsNullOrEmpty(update.CurrentPassword) || hasher.VerifyHashedPassword(user, user.PasswordHash, update.CurrentPassword) == PasswordVerificationResult.Failed)
                throw new System.ComponentModel.DataAnnotations.ValidationException("Para cambiar tu correo, introduce tu contraseña actual.");
            user.EmailConfirmed = false; user.SecurityStamp = Guid.NewGuid();
        }
        Apply(user, dto); await Save(ct);
        return Response(user);
    }
}
