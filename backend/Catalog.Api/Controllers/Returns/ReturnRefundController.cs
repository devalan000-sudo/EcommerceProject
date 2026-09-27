using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles = "Admin")]
public class ReturnRefundController(ReturnRefundService service) : ControllerBase
{
    [HttpPost("returns/{id:guid}/refunds")]
    public Task<ActionResult<ReturnRefundDto>> Create(Guid id, ReturnRefundCreateDto dto, CancellationToken ct) => Execute(actor => service.CreateAsync(id, actor, dto, ct));
    [HttpPost("return-refunds/{id:guid}/refresh")]
    public Task<ActionResult<ReturnRefundDto>> Refresh(Guid id, ReturnRefundRefreshDto dto, CancellationToken ct) => Execute(_ => service.ReconcileAsync(id, dto.StripeRefundId, ct));

    private async Task<ActionResult<ReturnRefundDto>> Execute(Func<int, Task<ReturnRefundDto?>> action)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) || actor <= 0) return Unauthorized();
        try { var result = await action(actor); return result is null ? NotFound() : Ok(result); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "El estado cambió. Actualiza el historial antes de continuar." }); }
        catch (SqlException ex) when (ex.Number == 1205) { return Conflict(new { message = "Otra operación está en curso. Actualiza e inténtalo nuevamente." }); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException or KeyNotFoundException)
        { return StatusCode(503, new { message = "Stripe de prueba no está disponible o no confirmó la operación. Actualiza el historial; si quedó pendiente, el sistema volverá a consultarla. No registres otro reembolso externo." }); }
    }
}
