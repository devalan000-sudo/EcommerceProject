using System.Net.Mail;
using System.Net.Http.Json;
using System.Text.Json;

namespace Catalog.Api.Services;

public class EmailSettings(IConfiguration config)
{
    // Indica si el proveedor y la dirección remitente están configurados correctamente.
    public bool Ready => config.GetValue<bool>("Postmark:Enabled") && !string.IsNullOrWhiteSpace(config["Postmark:ServerToken"]) && ValidAddress(config["Postmark:From"]) && ValidUrl(config["Email:FrontendUrl"] ?? "http://localhost:5173");
    public string FrontendUrl => (config["Email:FrontendUrl"] ?? "http://localhost:5173").TrimEnd('/');
    public bool RequireConfirmed => config.GetValue<bool>("Email:RequireConfirmedForCheckout");
    // Valida que una dirección de correo sea individual y no una lista.
    public static bool ValidAddress(string? value) => MailAddress.TryCreate(value, out var address) && address.Address == value && !value.Contains(',');
    private static bool ValidUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback)) && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}

public class PostmarkGateway(HttpClient http, IConfiguration config, EmailSettings settings)
{
    // Envía un correo transaccional a Postmark usando el stream configurado.
    public async Task<string> SendAsync(string to, string subject, string text, Guid id, CancellationToken ct)
    {
        if (!settings.Ready || !EmailSettings.ValidAddress(to)) throw new InvalidOperationException("Correo no configurado o destinatario inválido.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.postmarkapp.com/email");
        request.Headers.Add("X-Postmark-Server-Token", config["Postmark:ServerToken"]);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = JsonContent.Create(new
        {
            From = config["Postmark:From"],
            To = to,
            Subject = subject,
            TextBody = text,
            MessageStream = config["Postmark:MessageStream"] ?? "outbound",
            TrackOpens = false,
            TrackLinks = "None",
            Metadata = new { outbox_id = id.ToString() }
        }, options: new JsonSerializerOptions { PropertyNamingPolicy = null });
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Postmark no confirmó el envío.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (json.RootElement.GetProperty("ErrorCode").GetInt32() != 0) throw new HttpRequestException("Postmark rechazó el envío.");
        return json.RootElement.GetProperty("MessageID").GetString() ?? throw new HttpRequestException("Falta confirmación del proveedor.");
    }
}
