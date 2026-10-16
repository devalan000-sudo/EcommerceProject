using System.ComponentModel.DataAnnotations;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/admin/orders"), Authorize(Roles = "Admin")]
public class AdminOrderController(AdminOrderService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminOrderPage>> List([FromQuery] AdminOrderQuery query, CancellationToken ct)
    {
        try { return Ok(await service.ListAsync(query, ct)); }
        catch (ValidationException e) { return BadRequest(new { message = e.Message }); }
    }

    [HttpPut("{id:guid}/fulfillment")]
    public async Task<ActionResult<OrderResponseDto>> Update(Guid id, FulfillmentUpdateDto dto, CancellationToken ct)
    {
        try { var order = await service.UpdateAsync(id, dto, ct); return order is null ? NotFound() : Ok(order); }
        catch (ValidationException e) { return Conflict(new { message = e.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "El pedido cambió. Actualiza el detalle antes de guardar nuevamente." }); }
    }
}
