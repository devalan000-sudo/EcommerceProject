using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/admin/images"), Authorize(Roles = "Admin")]
public class ImageUploadController(CloudinaryService service) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { configured = service.Configured, maxBytes = CloudinaryService.MaxBytes });

    [HttpPost, EnableRateLimiting("image-uploads")]
    [RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<ActionResult<ImageUploadResult>> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        try { return Ok(await service.UploadAsync(file, ct)); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException or KeyNotFoundException)
        { return StatusCode(503, new { message = "No se confirmó la subida. Revisa la configuración de Cloudinary o intenta nuevamente. La imagen del producto no se ha cambiado." }); }
    }
}
