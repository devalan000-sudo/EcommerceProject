using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Catalog.Api.Services;
using Xunit;

public class StripeWebhookTests
{
    private const string Secret = "unit-test-signature-secret";
    private static string Sign(string body, long timestamp) => $"t={timestamp},v1={Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")))}";

    [Fact]
    public void ValidSignatureAcceptsRawBody()
    {
        var now = DateTimeOffset.UtcNow; const string body = "{\"livemode\":false}";
        Assert.False(StripeWebhook.Verify(body, Sign(body, now.ToUnixTimeSeconds()), Secret, now).GetProperty("livemode").GetBoolean());
    }

    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(false, -301, false)]
    [InlineData(false, 0, true)]
    public void RejectsLiveExpiredAndTamperedEvents(bool live, int offset, bool tampered)
    {
        var now = DateTimeOffset.UtcNow;
        var body = live ? "{\"livemode\":true}" : "{\"livemode\":false}";
        var signature = Sign(body, now.AddSeconds(offset).ToUnixTimeSeconds());
        Assert.Throws<ValidationException>(() => StripeWebhook.Verify(tampered ? body + " " : body, signature, Secret, now));
    }
}
