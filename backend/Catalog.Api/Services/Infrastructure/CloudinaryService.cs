using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Catalog.Api.Services;

public record ImageUploadResult(string ImageUrl, string PublicId);

public class CloudinaryService(HttpClient http, IConfiguration config)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public bool Configured => Regex.IsMatch(config["Cloudinary:CloudName"] ?? "", "^[a-zA-Z0-9_-]+$") &&
        !string.IsNullOrWhiteSpace(config["Cloudinary:ApiKey"]) && !string.IsNullOrWhiteSpace(config["Cloudinary:ApiSecret"]);

    public static string DetectFormat(byte[] bytes)
    {
        if (bytes.Length >= 12 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "jpg";
        if (bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        if (bytes.Length >= 16 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return "webp";
        throw new ValidationException("Selecciona una imagen JPG, PNG o WebP válida.");
    }

    public async Task<ImageUploadResult> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (!Configured) throw new InvalidOperationException("Cloudinary no está configurado.");
        if (file.Length <= 0 || file.Length > MaxBytes) throw new ValidationException("La imagen debe pesar entre 1 byte y 5 MB.");
        // Lectura acotada incluso si el tamaño declarado no coincide.
        await using var source = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var block = new byte[81920]; int read;
        while ((read = await source.ReadAsync(block.AsMemory(), ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) throw new ValidationException("La imagen supera 5 MB.");
            buffer.Write(block, 0, read);
        }
        var bytes = buffer.ToArray(); var format = DetectFormat(bytes);
        var publicId = "catalog/products/" + Guid.NewGuid().ToString("N");
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["allowed_formats"] = "jpg,png,webp",
            ["overwrite"] = "false",
            ["public_id"] = publicId,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
        };
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("&", values.Select(v => $"{v.Key}={v.Value}")) + config["Cloudinary:ApiSecret"]))).ToLowerInvariant();
        using var body = new MultipartFormDataContent();
        foreach (var pair in values) body.Add(new StringContent(pair.Value), pair.Key);
        body.Add(new StringContent(config["Cloudinary:ApiKey"]!), "api_key");
        body.Add(new StringContent(signature), "signature");
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new MediaTypeHeaderValue(format == "jpg" ? "image/jpeg" : $"image/{format}");
        body.Add(image, "file", $"product.{format}");
        using var response = await http.PostAsync($"https://api.cloudinary.com/v1_1/{config["Cloudinary:CloudName"]}/image/upload", body, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Cloudinary no confirmó la carga.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        var url = root.GetProperty("secure_url").GetString();
        if (root.GetProperty("public_id").GetString() != publicId || root.GetProperty("resource_type").GetString() != "image" ||
            !new[] { "jpg", "png", "webp" }.Contains(root.GetProperty("format").GetString()) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "res.cloudinary.com" || uri.UserInfo.Length > 0)
            throw new InvalidOperationException("Cloudinary devolvió una respuesta no válida.");
        return new(uri.AbsoluteUri, publicId);
    }
}
