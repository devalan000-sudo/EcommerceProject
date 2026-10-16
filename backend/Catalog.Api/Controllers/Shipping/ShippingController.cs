using System.ComponentModel.DataAnnotations;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/shipping-options")]
public class ShippingController(ShippingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ShippingResponseDto>>> List(CancellationToken ct) => await service.ListAsync(User.IsInRole("Admin"), ct);
    [HttpPost, Authorize(Roles = "Admin")]
    public Task<ActionResult<ShippingResponseDto>> Create(ShippingWriteDto dto, CancellationToken ct) => Save(null, dto, ct);
    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public Task<ActionResult<ShippingResponseDto>> Update(int id, ShippingWriteDto dto, CancellationToken ct) => Save(id, dto, ct);
    private async Task<ActionResult<ShippingResponseDto>> Save(int? id, ShippingWriteDto dto, CancellationToken ct)
    {
        try { var result = await service.SaveAsync(id, dto, ct); return result is null ? NotFound() : Ok(result); }
        catch (ValidationException e) { return BadRequest(new { message = e.Message }); }
    }
}
