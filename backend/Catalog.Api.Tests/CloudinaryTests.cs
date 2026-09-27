using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

public class CloudinaryTests
{
    [Fact]
    public void RejectsHtmlAndEmptyFilesInsteadOfTrustingExtension()
    {
        Assert.Throws<ValidationException>(() => CloudinaryService.DetectFormat(Encoding.UTF8.GetBytes("<html>not an image</html>")));
        Assert.Throws<ValidationException>(() => CloudinaryService.DetectFormat([]));
    }
    [Fact]
    public async Task UploadReturnsOnlyPublicResultAndUsesServerCredentials()
    {
        var handler = new FakeCloudinary(); using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "unit-test",
            ["Cloudinary:ApiKey"] = "test-key",
            ["Cloudinary:ApiSecret"] = "test-secret"
        }).Build();
        var service = new CloudinaryService(http, config);
        var bytes = new byte[24]; new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        using var stream = new MemoryStream(bytes);
        var result = await service.UploadAsync(new FormFile(stream, 0, bytes.Length, "file", "private-name.png"), default);
        Assert.StartsWith("https://res.cloudinary.com/", result.ImageUrl);
        Assert.StartsWith("catalog/products/", result.PublicId);
        Assert.DoesNotContain("test-secret", JsonSerializer.Serialize(result));
        Assert.True(handler.Signed);
    }
    [Fact]
    public async Task OversizedFileNeverReachesProvider()
    {
        using var http = new HttpClient(new FakeCloudinary());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Cloudinary:CloudName"] = "test", ["Cloudinary:ApiKey"] = "key", ["Cloudinary:ApiSecret"] = "secret" }).Build();
        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<ValidationException>(() => new CloudinaryService(http, config).UploadAsync(new FormFile(stream, 0, CloudinaryService.MaxBytes + 1, "file", "x.png"), default));
    }
    [Fact]
    public void EndpointIsAdminOnly() => Assert.Contains(typeof(Catalog.Api.Controllers.ImageUploadController)
        .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(), a => a.Roles == "Admin");
    private class FakeCloudinary : HttpMessageHandler
    {
        public bool Signed;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("api.cloudinary.com", request.RequestUri!.Host);
            var form = (MultipartFormDataContent)request.Content!;
            var values = new Dictionary<string, string>();
            foreach (var part in form)
            {
                var name = part.Headers.ContentDisposition!.Name!.Trim('"');
                if (name != "file") values[name] = await part.ReadAsStringAsync(ct);
            }
            Assert.Equal("test-key", values["api_key"]); Assert.False(values.ContainsKey("api_secret"));
            Signed = values["signature"].Length == 64;
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { public_id = values["public_id"], secure_url = "https://res.cloudinary.com/unit-test/image/upload/test.png", resource_type = "image", format = "png" })) };
        }
    }
}
