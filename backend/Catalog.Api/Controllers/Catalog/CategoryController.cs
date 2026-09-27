using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/categories")]
public class CategoryController(CategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponseDto>>> List(CancellationToken ct) => await service.ListAsync(ct);

    [Authorize(Roles = "Admin"), HttpPost]
    public async Task<ActionResult<CategoryResponseDto>> Create(CategoryWriteDto dto, CancellationToken ct)
    {
        var category = await service.CreateAsync(dto, ct);
        return Created("/api/categories", category);
    }

    [Authorize(Roles = "Admin"), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryWriteDto dto, CancellationToken ct) =>
        await service.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    [Authorize(Roles = "Admin"), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) switch
        {
            "missing" => NotFound(),
            "in-use" => Conflict(new { message = "Mueve o elimina los productos de esta categoría antes de eliminarla." }),
            _ => NoContent()
        };
}

