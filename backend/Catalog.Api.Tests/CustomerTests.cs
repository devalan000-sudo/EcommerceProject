using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Catalog.Api.Controllers;
using Catalog.Api.Data;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

public class CustomerTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CatalogDbContext db;
    private readonly CustomerService service;
    private readonly TokenService tokens;
    public CustomerTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        tokens = new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-only-key-at-least-thirty-two-bytes-long",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test"
        }).Build());
        service = new(db, new PasswordHasher<Customer>(), tokens);
    }
    private static CustomerRegisterDto User(string email) => new()
    {
        FirstName = "Ana",
        LastName = "López",
        Email = email,
        Address = "Calle Uno 123, Centro, Ciudad, CP 12345, México",
        Password = "Test-password-1234"
    };
    [Fact]
    public async Task RegistrationDoesNotRequireAddressAndProfileEditPreservesLegacyAddress()
    {
        var dto = User("address@example.test"); dto.Address = null;
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(dto, new(dto), true);
        await service.RegisterAsync(dto, default);
        var user = await db.Customers.SingleAsync();
        Assert.Equal("", user.Address);
        user.Address = "Dirección histórica"; await db.SaveChangesAsync();
        await service.UpdateAsync(user.Id, new CustomerProfileDto { FirstName = "Ana", LastName = "López", Email = user.Email }, default);
        Assert.Equal("Dirección histórica", user.Address);
    }

    [Fact]
    public async Task RegistrationNormalizesEmailHashesPasswordAndRejectsDuplicates()
    {
        var registration = await service.RegisterAsync(User(" Ana@Example.com "), default);
        var user = await db.Customers.SingleAsync();
        Assert.Equal("ANA@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Null(user.Phone);
        Assert.NotEqual("Test-password-1234", user.PasswordHash);
        Assert.True(registration.RequiresEmailConfirmation);
        Assert.Null(await service.LoginAsync(new("ANA@example.com", "Test-password-1234"), default));
        user.EmailConfirmed = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DuplicateEmailException>(() => service.RegisterAsync(User("ana@example.com"), default));
        Assert.NotNull(await service.LoginAsync(new("ANA@example.com", "Test-password-1234"), default));
        Assert.Null(await service.LoginAsync(new("ana@example.com", "incorrect"), default));
    }
    [Fact]
    public async Task ProfileUpdatePreservesOtherUserAndRejectsTakenEmail()
    {
        await service.RegisterAsync(User("one@example.com"), default);
        await service.RegisterAsync(User("two@example.com"), default);
        var users = await db.Customers.OrderBy(c => c.Id).ToListAsync();
        var edit = new CustomerProfileDto { FirstName = "Nuevo", LastName = "Nombre", Email = "ONE@example.com", Phone = "5512345678", Address = "Nueva dirección 123, Ciudad" };
        var updated = await service.UpdateAsync(users[0].Id, edit, default);
        Assert.Equal("Nuevo", updated!.FirstName);
        Assert.Equal("Ana", (await service.GetAsync(users[1].Id, default))!.FirstName);
        edit.Email = "TWO@example.com";
        await Assert.ThrowsAsync<DuplicateEmailException>(() => service.UpdateAsync(users[0].Id, edit, default));
        Assert.Null(await service.UpdateAsync(999, edit, default));
    }
    [Fact]
    public async Task DatabaseEnforcesUniqueNormalizedEmail()
    {
        await service.RegisterAsync(User("one@example.com"), default);
        db.Customers.Add(new Customer { Email = "ONE@example.com", NormalizedEmail = "ONE@EXAMPLE.COM" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public void WriteEndpointsRequireAdminAndProfileRequiresCustomer()
    {
        foreach (var type in new[] { typeof(ProductController), typeof(CategoryController) })
            foreach (var name in new[] { "Create", "Update", "Delete" })
                Assert.Equal("Admin", type.GetMethod(name)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        foreach (var name in new[] { "Me", "Update" })
            Assert.Equal("Customer", typeof(CustomerController).GetMethod(name)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
    }
    [Fact]
    public async Task AdminLoginProducesAdminRole()
    {
        var admin = new AdminUser { Username = "admin" };
        var hasher = new PasswordHasher<AdminUser>();
        admin.PasswordHash = hasher.HashPassword(admin, "admin-test-password");
        db.AdminUsers.Add(admin); await db.SaveChangesAsync();
        var auth = new AuthService(db, hasher, tokens);
        var session = await auth.LoginAsync(new("admin", "admin-test-password"), default);
        Assert.Equal("Admin", new JwtSecurityTokenHandler().ReadJwtToken(session!.Token).Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
