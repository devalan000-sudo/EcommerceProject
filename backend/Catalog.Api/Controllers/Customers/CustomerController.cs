using System.Security.Claims;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Catalog.Api.Controllers;

[ApiController, Route("api/customers")]
public class CustomerController(CustomerService service) : ControllerBase
{
    [HttpPost("register"), EnableRateLimiting("login")]
    public async Task<ActionResult<RegistrationResponseDto>> Register(CustomerRegisterDto dto, CancellationToken ct)
    {
        try { return StatusCode(201, await service.RegisterAsync(dto, ct)); }
        catch (DuplicateEmailException) { return Conflict(new { message = "Este correo electrónico ya está registrado." }); }
        catch (InvalidOperationException e) { return StatusCode(503, new { message = e.Message }); }
    }
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(CustomerLoginDto dto, CancellationToken ct)
    {
        var result = await service.LoginAsync(dto, ct);
        if (result is not null) return Ok(result);
        return Unauthorized(new { message = "Correo o contraseña incorrectos, o el correo aún no ha sido confirmado." });
    }
    [Authorize(Roles = "Customer"), HttpGet("me")]
    public async Task<ActionResult<CustomerResponseDto>> Me(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var result = await service.GetAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }
    [Authorize(Roles = "Customer"), HttpPut("me")]
    public async Task<ActionResult<CustomerResponseDto>> Update(CustomerUpdateDto dto, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        try { var result = await service.UpdateAsync(id, dto, ct); return result is null ? NotFound() : Ok(result); }
        catch (DuplicateEmailException) { return Conflict(new { message = "Este correo electrónico ya está registrado." }); }
        catch (System.ComponentModel.DataAnnotations.ValidationException e) { return BadRequest(new { message = e.Message }); }
    }
}
