using System.Net;
using Catalog.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.Api.Services;

public static class DeploymentConfiguration
{
    public static void AddDeployment(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var keyPath = config["DataProtection:KeysPath"];
        if (!builder.Environment.IsDevelopment())
        {
            foreach (var name in new[] { "ConnectionStrings:DefaultConnection", "Jwt:Issuer", "Jwt:Audience", "DataProtection:KeysPath", "DataProtection:ApplicationName" })
                if (string.IsNullOrWhiteSpace(config[name])) throw new InvalidOperationException($"Configura {name} para este despliegue.");
        }
        var protection = builder.Services.AddDataProtection().SetApplicationName(config["DataProtection:ApplicationName"] ?? "Catalog.Api");
        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            if (!Path.IsPathFullyQualified(keyPath)) throw new InvalidOperationException("DataProtection:KeysPath debe ser una ruta absoluta persistente.");
            Directory.CreateDirectory(keyPath);
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        }
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var value in (config["Proxy:KnownProxies"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!IPAddress.TryParse(value, out var address)) throw new InvalidOperationException("Proxy:KnownProxies solo admite direcciones IP separadas por coma.");
                options.KnownProxies.Add(address);
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) options.KnownProxies.Add(address.MapToIPv6());
            }
        });
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
        builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);
    }
}

public class DatabaseReadinessCheck(CatalogDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(ct) || (await db.Database.GetPendingMigrationsAsync(ct)).Any()) return HealthCheckResult.Unhealthy();
            return HealthCheckResult.Healthy();
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return HealthCheckResult.Unhealthy(); }
    }
}
