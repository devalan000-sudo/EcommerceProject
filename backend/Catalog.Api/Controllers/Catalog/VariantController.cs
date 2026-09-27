using System.ComponentModel.DataAnnotations;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/products/{productId:int}/variants")]
public class VariantController(InventoryService service, VariantTemplateService templates) : ControllerBase
{
    [HttpPost("template"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApplyTemplate(int productId, VariantTemplateRequest dto, CancellationToken ct)
    {
        try { return Ok(new { added = await templates.ApplyAsync(productId, dto, ct) }); }
        catch (ValidationException e) { return BadRequest(new { message = e.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "Recarga las variantes y vuelve a intentar." }); }
    }
    [HttpGet]
    public async Task<ActionResult<List<VariantResponseDto>>> List(int productId, CancellationToken ct) =>
        await service.ListAsync(productId, User.IsInRole("Admin"), ct);

    [HttpPost, Authorize(Roles = "Admin")]
    public Task<ActionResult<VariantResponseDto>> Create(int productId, VariantWriteDto dto, CancellationToken ct) => Save(productId, null, dto, ct);
    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public Task<ActionResult<VariantResponseDto>> Update(int productId, int id, VariantWriteDto dto, CancellationToken ct) => Save(productId, id, dto, ct);
    private async Task<ActionResult<VariantResponseDto>> Save(int productId, int? id, VariantWriteDto dto, CancellationToken ct)
    {
        try { return Ok(await service.SaveVariantAsync(productId, id, dto, ct)); }
        catch (ValidationException e) { return Conflict(new { message = e.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "No se pudo guardar la variante. Recarga y comprueba que no esté duplicada." }); }
    }
}
