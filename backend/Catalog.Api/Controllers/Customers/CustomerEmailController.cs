using System.ComponentModel.DataAnnotations;
using Catalog.Api.Services;
using Catalog.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

public record EmailRequestDto([Required, EmailAddress, StringLength(254)] string Email);
public record EmailTokenDto([Required, StringLength(64, MinimumLength = 64)] string Token);
public record ResetPasswordDto([Required, StringLength(64, MinimumLength = 64)] string Token, [Required, StringLength(256, MinimumLength = 12)] string Password);

[ApiController, Route("api/customers/email"), EnableRateLimiting("login")]
public class CustomerEmailController(CustomerEmailService service, EmailSettings settings) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled = settings.Ready, requireConfirmedForCheckout = settings.RequireConfirmed });
    private async Task<IActionResult> QueueRequest(EmailRequestDto dto, string purpose, CancellationToken ct)
    {
        try { await service.RequestAsync(dto.Email, purpose, ct); return Ok(new { message = "Si la cuenta requiere esta acción, recibirás un correo con los pasos a seguir." }); }
        catch (InvalidOperationException) { return StatusCode(503, new { message = "El servicio de correo todavía no está configurado." }); }
    }
    [HttpPost("resend-confirmation")]
    public Task<IActionResult> Resend(EmailRequestDto dto, CancellationToken ct) => QueueRequest(dto, "confirm", ct);
    [HttpPost("forgot-password")]
    public Task<IActionResult> Forgot(EmailRequestDto dto, CancellationToken ct) => QueueRequest(dto, "reset", ct);
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(EmailTokenDto dto, CancellationToken ct) => await Consume(dto.Token, "confirm", null, ct);
    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetPasswordDto dto, CancellationToken ct) => await Consume(dto.Token, "reset", dto.Password, ct);
    private async Task<IActionResult> Consume(string token, string purpose, string? password, CancellationToken ct)
    {
        try { return await service.ConsumeAsync(token, purpose, password, ct) ? Ok(new { message = purpose == "confirm" ? "Correo confirmado." : "Contraseña actualizada. Inicia sesión nuevamente." }) : BadRequest(new { message = "El enlace es inválido, venció o ya fue utilizado. Solicita otro." }); }
        catch (DbUpdateConcurrencyException) { return BadRequest(new { message = "El enlace ya no es válido. Solicita otro." }); }
        catch (ValidationException e) { return BadRequest(new { message = e.Message }); }
    }
}

[ApiController, Route("api/admin/email-status"), Authorize(Roles = "Admin")]
public class EmailStatusController(CatalogDbContext db, EmailSettings settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(new
    {
        enabled = settings.Ready,
        counts = await db.EmailOutbox.AsNoTracking().GroupBy(m => m.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct)
    });
}
