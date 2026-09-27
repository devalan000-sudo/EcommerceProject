using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/store-settings")]
public class StoreSettingsController(StoreSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StoreSettingsResponseDto>> Get(CancellationToken ct)
    {
        var settings = await service.GetAsync(ct);
        return settings is null ? NoContent() : Ok(settings);
    }

    [HttpPut, Authorize(Roles = "Admin")]
    public async Task<ActionResult<StoreSettingsResponseDto>> Update(StoreSettingsWriteDto dto, CancellationToken ct)
    {
        try { return Ok(await service.SaveAsync(dto, ct)); }
        catch (DbUpdateException) { return Conflict(new { message = "No se pudo guardar. Recarga la configuración y revisa si otro administrador la modificó." }); }
    }
}
