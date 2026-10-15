using System.Security.Claims;
using Catalog.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public static class CustomerSessionValidation
{
    // Comprueba que el sello del token siga coincidiendo con la sesión almacenada.
    public static bool Matches(Guid stamp, string? claim) => claim is null ? stamp == Guid.Empty : Guid.TryParse(claim, out var parsed) && parsed == stamp;
    // Invalida tokens de clientes cuyo sello de seguridad ya cambió.
    public static async Task Validate(TokenValidatedContext context)
    {
        if (context.Principal?.IsInRole("Customer") != true) return;
        var db = context.HttpContext.RequestServices.GetRequiredService<CatalogDbContext>();
        if (!int.TryParse(context.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { context.Fail("Sesión inválida."); return; }
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, context.HttpContext.RequestAborted);
        if (customer is null || !Matches(customer.SecurityStamp, context.Principal.FindFirstValue("security_stamp"))) context.Fail("La sesión fue invalidada.");
    }
}

public class ConfirmedEmailRequirement : IAuthorizationRequirement { }
public class ConfirmedEmailHandler(CatalogDbContext db, EmailSettings settings) : AuthorizationHandler<ConfirmedEmailRequirement>
{
    // Permite continuar solo a clientes con correo confirmado cuando la política lo exige.
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ConfirmedEmailRequirement requirement)
    {
        if (!settings.RequireConfirmed) { context.Succeed(requirement); return; }
        if (int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
            await db.Customers.AnyAsync(c => c.Id == id && c.EmailConfirmed)) context.Succeed(requirement);
    }
}
