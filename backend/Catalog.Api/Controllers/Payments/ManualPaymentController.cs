using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/admin/orders"), Authorize(Roles = "Admin")]
public class ManualPaymentController(ManualPaymentService service) : ControllerBase
{
    [HttpPost("{id:guid}/manual-payment")]
    public async Task<ActionResult<OrderResponseDto>> Confirm(Guid id, ManualPaymentDto dto, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) || adminId <= 0) return Unauthorized();
        try { var result = await service.ConfirmAsync(id, adminId, dto, ct); return result is null ? NotFound() : Ok(result); }
        catch (ValidationException e) { return Conflict(new { message = e.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "El pedido cambió. Actualiza el detalle antes de confirmar." }); }
    }
}
