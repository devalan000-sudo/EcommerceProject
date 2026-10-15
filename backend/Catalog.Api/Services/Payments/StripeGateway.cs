using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Catalog.Api.Entities;

namespace Catalog.Api.Services;

public class StripeGateway(HttpClient http, IConfiguration config)
{
    // Indica si existen credenciales de Stripe en modo prueba.
    public bool Enabled => config["Stripe:SecretKey"]?.StartsWith("sk_test_", StringComparison.Ordinal) == true
        && config["Stripe:WebhookSecret"]?.StartsWith("whsec_", StringComparison.Ordinal) == true;

    // Detiene la operación si Stripe no está configurado exclusivamente para pruebas.
    public void RequireConfigured()
    {
        if (!Enabled) throw new InvalidOperationException("Configura Stripe:SecretKey de prueba y Stripe:WebhookSecret en los secretos del backend.");
    }

    // Valida y devuelve la URL que Stripe usará para regresar al checkout.
    public string FrontendUrl()
    {
        var value = config["Stripe:FrontendUrl"] ?? "http://localhost:5173";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException("Configura Stripe:FrontendUrl con HTTPS o localhost.");
        return value.TrimEnd('/');
    }

    // Envía una petición autenticada a Stripe y rechaza respuestas de producción.
    private async Task<JsonElement> SendAsync(HttpMethod method, string path, IEnumerable<KeyValuePair<string, string>>? values, string? key, CancellationToken ct, bool requireLiveModeField = true)
    {
        RequireConfigured();
        using var request = new HttpRequestMessage(method, "https://api.stripe.com/v1/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["Stripe:SecretKey"]);
        request.Headers.Add("Stripe-Version", "2024-06-20");
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (values is not null) request.Content = new FormUrlEncodedContent(values);
        using var response = await http.SendAsync(request, ct);
        // No exponer respuestas, encabezados ni credenciales del proveedor en logs.
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Stripe no confirmó la operación. Intenta consultar el pedido de nuevo.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var result = json.RootElement.Clone();
        if (result.TryGetProperty("livemode", out var live) ? live.GetBoolean() : requireLiveModeField)
            throw new InvalidOperationException("Solo se admiten sesiones Stripe de prueba.");
        return result;
    }

    // Crea una sesión de Checkout con el total congelado del pedido.
    public Task<JsonElement> CreateAsync(Order order, CancellationToken ct)
    {
        var frontend = order.StripeFrontendUrl ?? FrontendUrl();
        if (!Uri.TryCreate(frontend, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new InvalidOperationException("Configura Stripe:FrontendUrl con HTTPS o localhost.");
        var values = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["payment_method_types[0]"] = "card",
            ["success_url"] = $"{frontend.TrimEnd('/')}/checkout?order={order.Id}&payment=return",
            ["cancel_url"] = $"{frontend.TrimEnd('/')}/checkout?order={order.Id}&payment=cancel",
            ["client_reference_id"] = order.Id.ToString(),
            ["metadata[order_id]"] = order.Id.ToString(),
            ["customer_email"] = order.Email,
            ["line_items[0][price_data][currency]"] = "mxn",
            ["line_items[0][price_data][product_data][name]"] = $"Pedido {order.Number} (productos y entrega)",
            ["line_items[0][price_data][unit_amount]"] = decimal.ToInt64(order.Total!.Value * 100).ToString(CultureInfo.InvariantCulture),
            ["line_items[0][quantity]"] = "1"
        };
        return SendAsync(HttpMethod.Post, "checkout/sessions", values, $"checkout-{order.Id:N}", ct);
    }
    // Consulta una sesión de Checkout existente.
    public Task<JsonElement> GetAsync(string id, CancellationToken ct) => SendAsync(HttpMethod.Get, $"checkout/sessions/{Uri.EscapeDataString(id)}", null, null, ct);

    // Expira una sesión abierta usando una clave idempotente.
    public Task<JsonElement> ExpireAsync(string id, CancellationToken ct) => SendAsync(HttpMethod.Post, $"checkout/sessions/{Uri.EscapeDataString(id)}/expire", [], $"expire-{id}", ct);
    // Refund no incluye livemode. Solo se usa tras validar la sesión y con sk_test_ obligatoria.
    // Solicita un reembolso total del pedido.
    public Task<JsonElement> RefundAsync(Order order, CancellationToken ct) => SendAsync(HttpMethod.Post, "refunds", new Dictionary<string, string>
    {
        ["payment_intent"] = order.RefundPaymentIntentId!,
        ["amount"] = decimal.ToInt64(order.Total!.Value * 100).ToString(CultureInfo.InvariantCulture),
        ["metadata[order_id]"] = order.Id.ToString(),
        ["metadata[request_id]"] = order.RefundRequestId!.Value.ToString()
    }, $"refund-{order.Id:N}-{order.RefundRequestId:N}", ct, false);
    // Consulta un reembolso existente para completar una reconciliación.
    public Task<JsonElement> GetRefundAsync(string id, CancellationToken ct) => SendAsync(HttpMethod.Get, $"refunds/{Uri.EscapeDataString(id)}", null, null, ct, false);

    // Solicita un reembolso parcial asociado a una devolución física.
    public Task<JsonElement> RefundReturnAsync(ReturnRefund refund, Guid orderId, CancellationToken ct) => SendAsync(HttpMethod.Post, "refunds", new Dictionary<string, string>
    {
        ["payment_intent"] = refund.PaymentIntentId!,
        ["amount"] = decimal.ToInt64(refund.Amount * 100).ToString(CultureInfo.InvariantCulture),
        ["metadata[order_id]"] = orderId.ToString(),
        ["metadata[return_refund_id]"] = refund.Id.ToString()
    }, $"return-refund-{refund.Id:N}", ct, false);

    // Verifica la firma y el modo de un webhook recibido de Stripe.
    public JsonElement VerifyWebhook(string body, string signature)
    {
        RequireConfigured();
        return StripeWebhook.Verify(body, signature, config["Stripe:WebhookSecret"]!, DateTimeOffset.UtcNow);
    }
}

public static class StripeWebhook
{
    // Valida timestamp, firma HMAC y que el evento pertenezca al modo de pruebas.
    public static JsonElement Verify(string body, string signature, string secret, DateTimeOffset now)
    {
        var parts = signature.Split(',').Select(p => p.Trim().Split('=', 2)).Where(p => p.Length == 2).ToList();
        var timestamps = parts.Where(p => p[0] == "t").ToList();
        if (timestamps.Count != 1 || !long.TryParse(timestamps[0][1], out var timestamp) || Math.Abs((decimal)now.ToUnixTimeSeconds() - timestamp) > 300)
            throw new ValidationException("Firma Stripe inválida o vencida.");
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        var valid = parts.Where(p => p[0] == "v1").Any(p =>
        {
            try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(p[1])); }
            catch (FormatException) { return false; }
        });
        if (!valid) throw new ValidationException("Firma Stripe inválida.");
        using var json = JsonDocument.Parse(body);
        var result = json.RootElement.Clone();
        if (!result.TryGetProperty("livemode", out var live) || live.GetBoolean()) throw new ValidationException("Evento no permitido: solo pruebas.");
        return result;
    }
}
