using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/products")]
public class ProductController(ProductService service) : ControllerBase
{
    [AllowAnonymous, HttpPost("{id:int}/view"), EnableRateLimiting("product-views")]
    public async Task<IActionResult> RegisterView(int id, CancellationToken ct) =>
        await service.RegisterViewAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet]
    public async Task<ActionResult<List<ProductResponseDto>>> List(CancellationToken ct) =>
        await service.ListAsync(User.IsInRole("Admin"), ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponseDto>> Get(int id, CancellationToken ct)
    {
        var product = await service.GetAsync(id, User.IsInRole("Admin"), ct);
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = "Admin"), HttpPost]
    public async Task<ActionResult<ProductResponseDto>> Create(ProductCreateDto dto, CancellationToken ct)
    {
        if (!await service.CategoryExistsAsync(dto.CategoryId, ct))
            return BadRequest(new { message = "La categoría no existe." });
        var product = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [Authorize(Roles = "Admin"), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductUpdateDto dto, CancellationToken ct)
    {
        if (await service.GetAsync(id, true, ct) is null) return NotFound();
        if (!await service.CategoryExistsAsync(dto.CategoryId, ct))
            return BadRequest(new { message = "La categoría no existe." });
        try { return await service.UpdateAsync(id, dto, ct) ? NoContent() : NotFound(); }
        catch (System.ComponentModel.DataAnnotations.ValidationException e) { return BadRequest(new { message = e.Message }); }
    }

    [Authorize(Roles = "Admin"), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try { return await service.DeleteAsync(id, ct) ? NoContent() : NotFound(); }
        catch (System.ComponentModel.DataAnnotations.ValidationException e) { return Conflict(new { message = e.Message }); }
    }
}

