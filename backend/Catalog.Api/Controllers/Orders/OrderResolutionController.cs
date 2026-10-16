using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/admin/orders"), Authorize(Roles = "Admin")]
public class OrderResolutionController(OrderResolutionService service, OrderService orders) : ControllerBase
{
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private async Task<ActionResult<OrderResponseDto>> Execute(Func<Task<OrderResponseDto?>> action)
    {
        if (AdminId <= 0) return Unauthorized();
        try { var result = await action(); return result is null ? NotFound() : Ok(result); }
        catch (ValidationException e) { return Conflict(new { message = e.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "El pedido cambió. Actualiza el detalle y revisa su estado." }); }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException)
        { return StatusCode(503, new { message = "No se confirmó la operación. Actualiza el detalle; si quedó pendiente, el sistema volverá a consultar Stripe." }); }
    }
    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<OrderResponseDto>> Cancel(Guid id, CancelOrderDto dto, CancellationToken ct) => Execute(() => service.CancelAsync(id, AdminId, dto, ct));
    [HttpPost("{id:guid}/refund")]
    public Task<ActionResult<OrderResponseDto>> Refund(Guid id, RefundOrderDto dto, CancellationToken ct) => Execute(() => service.RefundAsync(id, AdminId, dto, ct));
    [HttpPost("{id:guid}/refund/refresh")]
    public Task<ActionResult<OrderResponseDto>> Refresh(Guid id, CancellationToken ct) => Execute(async () => { await service.ReconcileRefundAsync(id, ct); return await orders.GetAsync(id, null, ct); });
    [HttpPost("{id:guid}/refund/restock")]
    public Task<ActionResult<OrderResponseDto>> Restock(Guid id, CancellationToken ct) => Execute(() => service.RetryRestockAsync(id, ct));
}
