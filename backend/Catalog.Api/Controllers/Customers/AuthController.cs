using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AuthService service) : ControllerBase
{
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto, CancellationToken ct)
    {
        var result = await service.LoginAsync(dto, ct);
        return result is null ? Unauthorized(new { message = "Credenciales incorrectas." }) : Ok(result);
    }
}
