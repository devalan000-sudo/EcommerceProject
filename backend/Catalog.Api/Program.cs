using System.Text;
using System.Threading.RateLimiting;
using Catalog.Api.Data;
using Catalog.Api.Entities;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Sonda del contenedor: no inicia workers ni carga secretos de la aplicación.
if (args.Contains("--healthcheck") || args.Contains("--healthcheck-ready"))
{
    try { using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) }; Environment.ExitCode = (await client.GetAsync("http://localhost:8080/health/" + (args.Contains("--healthcheck-ready") ? "ready" : "live"))).IsSuccessStatusCode ? 0 : 1; }
    catch { Environment.ExitCode = 1; }
    return;
}
var builder = WebApplication.CreateBuilder(args);
builder.AddDeployment();
var frontendBuild = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../frontend/dist"));
var emailSettings = new EmailSettings(builder.Configuration);
if (emailSettings.RequireConfirmed && !emailSettings.Ready)
    throw new InvalidOperationException("Configura Postmark y habilita el envío antes de activar Email:RequireConfirmedForCheckout.");
var key = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
    throw new InvalidOperationException($"No se pudo cargar Jwt:Key (mínimo 32 bytes). Entorno actual: {builder.Environment.EnvironmentName}. En Visual Studio selecciona el perfil http del proyecto Catalog.Api y configura Jwt:Key en Administrar secretos de usuario. En producción usa Jwt__Key.");

builder.Services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Configura ConnectionStrings:DefaultConnection.")));
builder.Services.AddScoped<CustomerAddressService>();
builder.Services.AddScoped<ReturnService>();
builder.Services.AddScoped<SeoService>();
builder.Services.AddHttpClient<CloudinaryService>(client => client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<ReturnRefundService>();
builder.Services.AddScoped<StoreSettingsService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<AdminOrderService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ManualPaymentService>();
builder.Services.AddScoped<OrderResolutionService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ShippingService>();
builder.Services.AddHttpClient<StripeGateway>(client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHostedService<PaymentReconciliationWorker>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddSingleton<EmailSettings>();
builder.Services.AddScoped<CustomerEmailService>();
builder.Services.AddHttpClient<PostmarkGateway>(client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<EmailDeliveryService>();
builder.Services.AddHostedService<EmailDeliveryWorker>();
builder.Services.AddScoped<IAuthorizationHandler, ConfirmedEmailHandler>();
builder.Services.AddScoped<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents { OnTokenValidated = CustomerSessionValidation.Validate };
});
builder.Services.AddAuthorization(options => options.AddPolicy("ConfirmedCustomer", policy => policy.RequireAuthenticatedUser().RequireRole("Customer").AddRequirements(new ConfirmedEmailRequirement())));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("image-uploads", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("product-views", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddScoped<VariantTemplateService>();
var app = builder.Build();
if (args.Contains("--seed-variant-template"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("La plantilla masiva sólo se ejecuta en Development.");
    using var scope = app.Services.CreateScope();
    var stock = builder.Configuration.GetValue<int>("Template:InitialStock");
    var count = await scope.ServiceProvider.GetRequiredService<VariantTemplateService>().ApplyCatalogAsync(stock, default);
    Console.WriteLine($"Variantes nuevas: {count}. Existencias previas conservadas.");
    return;
}
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
    return;
}

// Comando explícito de aprovisionamiento; no crea cuentas durante peticiones HTTP.
if (args.Contains("--seed-admin"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var username = builder.Configuration["SeedAdmin:Username"];
    var password = builder.Configuration["SeedAdmin:Password"];
    if (string.IsNullOrWhiteSpace(username) || username.Length > 100 ||
        string.IsNullOrEmpty(password) || password.Length < 12 || password.Length > 256)
        throw new InvalidOperationException("Configura SeedAdmin:Username (1–100) y SeedAdmin:Password (12–256 caracteres).");
    if (!await db.AdminUsers.AnyAsync(x => x.Username == username))
    {
        var admin = new AdminUser { Username = username };
        admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>().HashPassword(admin, password);
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
    }
    return;
}

app.UseExceptionHandler();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseHttpsRedirection());
}
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/index.html") { context.Response.Redirect("/", permanent: true); return; }
    await next();
});
if (Directory.Exists(frontendBuild) && !File.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot/index.html")))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendBuild) });
else app.UseStaticFiles();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapFallbackToController("Page", "Storefront");
app.Run();



