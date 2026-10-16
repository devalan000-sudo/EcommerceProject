using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Catalog.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public class TokenTests
{
    private const string TestKey = "test-only-signing-key-with-at-least-32-bytes";

    [Theory]
    [InlineData("Admin")]
    [InlineData("Customer")]
    public void TokenHasValidSignatureIdentityRoleAndOneHourExpiry(string role)
    {
        var service = new TokenService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = TestKey,
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience"
        }).Build());
        var before = DateTime.UtcNow;
        var result = service.Create(42, "test-user", role);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
            ValidateIssuer = true,
            ValidIssuer = "test-issuer",
            ValidateAudience = true,
            ValidAudience = "test-audience",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(result.Token, parameters, out var validated);
        Assert.Equal("42", principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        Assert.True(principal.IsInRole(role));
        Assert.InRange(result.ExpiresAtUtc, before.AddMinutes(60), DateTime.UtcNow.AddMinutes(60));
        Assert.InRange((result.ExpiresAtUtc - validated.ValidTo).TotalSeconds, 0, 1);
        var second = handler.ReadJwtToken(service.Create(42, "test-user", role).Token);
        Assert.NotEqual(handler.ReadJwtToken(result.Token).Id, second.Id);
        parameters.ValidAudience = "wrong-audience";
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => handler.ValidateToken(result.Token, parameters, out _));
    }
}
