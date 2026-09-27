using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/customers/addresses"), Authorize(Roles = "Customer")]
public class CustomerAddressController(CustomerAddressService service) : ControllerBase
{
    private int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => CustomerId <= 0 ? Unauthorized() : Ok(await service.ListAsync(CustomerId, ct));
    [HttpPost]
    public Task<IActionResult> Create(CustomerAddressWriteDto dto, CancellationToken ct) => Save(null, dto, ct);
    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, CustomerAddressWriteDto dto, CancellationToken ct) => Save(id, dto, ct);
    private Task<IActionResult> Save(int? id, CustomerAddressWriteDto dto, CancellationToken ct) => Execute(async () =>
    {
        var result = await service.SaveAsync(CustomerId, id, dto, ct);
        if (result is null) return NotFound();
        return id.HasValue ? Ok(result) : StatusCode(201, result);
    });
    [HttpDelete("{id:int}")]
    public Task<IActionResult> Delete(int id, DeleteCustomerAddressDto dto, CancellationToken ct) => Execute(async () =>
        await service.DeleteAsync(CustomerId, id, dto.Version, ct) ? NoContent() : NotFound());
    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        if (CustomerId <= 0) return Unauthorized();
        try { return await action(); }
        catch (ValidationException e) { return BadRequest(new { message = e.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "Las direcciones cambiaron. Recarga la lista antes de volver a guardar." }); }
        catch (SqlException e) when (e.Number == 1205) { return Conflict(new { message = "Otra operación actualizó tus direcciones. Recarga e inténtalo de nuevo." }); }
    }
}
