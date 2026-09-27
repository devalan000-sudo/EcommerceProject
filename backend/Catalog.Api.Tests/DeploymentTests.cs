using Catalog.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public class DeploymentTests
{
    [Fact]
    public void ProductionRequiresPersistentConfiguration()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.Sources.Clear();
        Assert.Throws<InvalidOperationException>(() => builder.AddDeployment());
    }
    [Fact]
    public void DevelopmentKeepsExistingDataProtectionIdentity()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.Sources.Clear(); builder.AddDeployment();
        using var services = builder.Services.BuildServiceProvider();
        Assert.Equal("Catalog.Api", services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
    }
    [Fact]
    public void TrustsOnlySpecifiedProxyAndDefaultLoopback()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Proxy:KnownProxies"] = "172.30.10.2" });
        builder.AddDeployment(); using var services = builder.Services.BuildServiceProvider();
        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Equal(1, options.ForwardLimit); Assert.Contains(System.Net.IPAddress.Parse("172.30.10.2"), options.KnownProxies);
        Assert.DoesNotContain(options.KnownNetworks, n => n.PrefixLength == 0);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
    }
    [Fact]
    public void RejectsRelativeKeyStorage()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["DataProtection:KeysPath"] = "relative-keys" });
        Assert.Throws<InvalidOperationException>(() => builder.AddDeployment());
    }
}
