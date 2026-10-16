using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/orders"), Authorize(Roles = "Customer,Admin")]
public class OrderController(OrderService service) : ControllerBase
{
    [HttpPost("quote"), Authorize(Roles = "Customer", Policy = "ConfirmedCustomer")]
    public async Task<ActionResult<OrderResponseDto>> Quote(OrderCreateDto dto, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId)) return Unauthorized();
        try { return Ok(await service.QuoteAsync(customerId, dto, ct)); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpPost, Authorize(Roles = "Customer", Policy = "ConfirmedCustomer")]
    public async Task<ActionResult<OrderResponseDto>> Create(OrderCreateDto dto, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId)) return Unauthorized();
        try
        {
            var order = await service.CreateAsync(customerId, dto, ct);
            return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponseDto>> Get(Guid id, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId)) return Unauthorized();
        var order = await service.GetAsync(id, User.IsInRole("Admin") ? null : customerId, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderSummaryDto>>> List([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId)) return Unauthorized();
        return await service.ListAsync(User.IsInRole("Admin") ? null : customerId, page, ct);
    }
}
