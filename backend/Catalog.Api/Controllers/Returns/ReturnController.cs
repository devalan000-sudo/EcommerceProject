using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

[ApiController, Route("api"), Authorize]
public class ReturnController(ReturnService service) : ControllerBase
{
    [HttpGet("orders/{orderId:guid}/return-options"), Authorize(Roles = "Customer")]
    public async Task<ActionResult<ReturnOptionsDto>> Options(Guid orderId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId)) return Unauthorized();
        var result = await service.OptionsAsync(orderId, customerId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("returns"), Authorize(Roles = "Customer")]
    public async Task<ActionResult<ReturnPageDto>> Mine([FromQuery] Guid orderId, [FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId)) return Unauthorized();
        return Ok(await service.ListAsync(customerId, orderId, null, page, ct));
    }

    [HttpGet("admin/returns"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReturnPageDto>> List([FromQuery] ReturnStatus? status, [FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        try { return Ok(await service.ListAsync(null, null, status, page, ct)); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("orders/{orderId:guid}/returns"), Authorize(Roles = "Customer")]
    public Task<ActionResult<ReturnResponseDto>> Create(Guid orderId, ReturnCreateDto dto, CancellationToken ct) =>
        Change(actor => service.CreateAsync(orderId, actor, dto, ct));

    [HttpPut("admin/returns/{id:guid}"), Authorize(Roles = "Admin")]
    public Task<ActionResult<ReturnResponseDto>> Review(Guid id, ReturnReviewDto dto, CancellationToken ct) =>
        Change(actor => service.ReviewAsync(id, actor, dto, ct));

    [HttpPost("admin/returns/{id:guid}/receipts"), Authorize(Roles = "Admin")]
    public Task<ActionResult<ReturnResponseDto>> Receive(Guid id, ReturnReceiveDto dto, CancellationToken ct) =>
        Change(actor => service.ReceiveAsync(id, actor, dto, ct));

    private async Task<ActionResult<ReturnResponseDto>> Change(Func<int, Task<ReturnResponseDto?>> operation)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        try { var result = await operation(actor); return result is null ? NotFound() : Ok(result); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "La solicitud cambió o ya existe. Actualiza antes de continuar." }); }
        catch (SqlException ex) when (ex.Number == 1205) { return Conflict(new { message = "Hubo otra operación simultánea. Actualiza e inténtalo de nuevo." }); }
    }
}
