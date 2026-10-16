using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController, Route("api/payments")]
public class PaymentController(PaymentService service, StripeGateway stripe) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled = stripe.Enabled, testMode = true });

    [HttpPost("{orderId:guid}/session"), Authorize(Roles = "Customer", Policy = "ConfirmedCustomer")]
    public async Task<IActionResult> Start(Guid orderId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        try { return Ok(await service.StartAsync(orderId, id, ct)); }
        catch (ValidationException e) { return Conflict(new { message = e.Message }); }
        catch (InvalidOperationException) { return StatusCode(503, new { message = "Stripe de prueba no está configurado o no está disponible." }); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "No se pudo confirmar la sesión. Reintenta desde este mismo pedido." }); }
    }

    [HttpPost("{orderId:guid}/cancel"), Authorize(Roles = "Customer")]
    public async Task<IActionResult> Cancel(Guid orderId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        try { await service.CancelAsync(orderId, id, ct); return NoContent(); }
        catch (ValidationException e) { return Conflict(new { message = e.Message }); }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException) { return StatusCode(503, new { message = "No se confirmó la cancelación. Se conserva la reserva hasta consultar Stripe." }); }
    }

    [HttpPost("stripe/webhook"), AllowAnonymous, RequestSizeLimit(1048576)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        if (!stripe.Enabled) return StatusCode(503);
        JsonElement notification;
        try
        {
            using var reader = new StreamReader(Request.Body);
            notification = stripe.VerifyWebhook(await reader.ReadToEndAsync(ct), Request.Headers["Stripe-Signature"].ToString());
        }
        catch (Exception e) when (e is ValidationException or JsonException or InvalidOperationException) { return BadRequest(); }
        var type = notification.GetProperty("type").GetString();
        if (type is not ("checkout.session.completed" or "checkout.session.expired" or "checkout.session.async_payment_succeeded")) return Ok();
        var session = notification.GetProperty("data").GetProperty("object");
        if (!session.TryGetProperty("metadata", out var metadata) || !metadata.TryGetProperty("order_id", out var value) || !Guid.TryParse(value.GetString(), out var orderId)) return Ok();
        // Los fallos de aplicación responden 5xx para que Stripe reintente; nunca confirmar antes de guardar.
        await service.ApplySessionAsync(orderId, session, ct);
        return Ok();
    }
}
