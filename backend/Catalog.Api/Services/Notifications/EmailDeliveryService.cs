using Catalog.Api.Data;
using Catalog.Api.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class EmailDeliveryService(CatalogDbContext db, PostmarkGateway postmark, EmailSettings settings, IDataProtectionProvider protection)
{
    public async Task ProcessAsync(Guid id, CancellationToken ct)
    {
        if (!settings.Ready) return;
        var now = DateTime.UtcNow;
        var claimed = await db.EmailOutbox.Where(m => m.Id == id && m.Status == "Pending" && m.NextAttemptUtc <= now && (m.LeaseUntilUtc == null || m.LeaseUntilUtc < now))
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.LeaseUntilUtc, now.AddMinutes(2)).SetProperty(m => m.Attempts, m => m.Attempts + 1), ct);
        if (claimed == 0) return;
        var mail = await db.EmailOutbox.SingleAsync(m => m.Id == id, ct);
        try
        {
            if (mail.ExpiresAtUtc <= now) { mail.Status = "Expired"; return; }
            string? to; string subject; string body;
            if (mail.TokenHash is not null)
            {
                var token = await db.CustomerEmailTokens.Include(t => t.Customer).SingleOrDefaultAsync(t => t.Hash == mail.TokenHash, ct);
                if (token is null || token.UsedAtUtc.HasValue || token.ExpiresAtUtc <= now || token.Customer.SecurityStamp != token.SecurityStamp || token.Customer.NormalizedEmail != token.NormalizedEmail || (token.Purpose == "confirm" && token.Customer.EmailConfirmed))
                { mail.Status = "Skipped"; return; }
                to = token.Customer.Email;
                var raw = protection.CreateProtector("customer-email-v1").Unprotect(mail.ProtectedToken!);
                var link = $"{settings.FrontendUrl}/account/security#action={token.Purpose}&token={Uri.EscapeDataString(raw)}";
                subject = token.Purpose == "confirm" ? "Confirma tu correo electrónico" : "Recupera tu contraseña";
                body = $"{subject}\n\nAbre este enlace y completa la acción:\n{link}\n\nEl enlace vence el {token.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC y solo puede utilizarse una vez. Si no lo solicitaste, ignora este correo.";
            }
            else
            {
                var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == mail.CustomerId, ct);
                if (customer is null) { mail.Status = "Skipped"; return; }
                if (!customer.EmailConfirmed) { mail.Attempts--; mail.NextAttemptUtc = now.AddMinutes(15); return; }
                var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == mail.OrderId && o.CustomerId == customer.Id, ct);
                if (order is null) { mail.Status = "Skipped"; return; }
                to = customer.Email;
                subject = mail.Kind switch { "OrderCreated" => "Recibimos tu pedido", "PaymentPaid" => "Pago registrado", "PaymentRefunded" => "Reembolso confirmado", "DeliveryShipped" => "Tu pedido fue enviado", "DeliveryDelivered" => "Tu pedido fue entregado", "DeliveryCancelled" => "Pedido cancelado", _ => "Actualización de tu pedido" };
                // Mensaje mínimo: el enlace autenticado muestra el estado actual y datos privados.
                body = $"{subject}\n\nPedido: {order.Number}\n\nConsulta el estado actual y los detalles iniciando sesión:\n{settings.FrontendUrl}/account?tab=orders&order={order.Id}\n\nEste aviso corresponde a un cambio registrado en el pedido; su estado puede haber avanzado.";
                if (AfterSalesEmails.Message(mail.Kind) is { } afterSales)
                {
                    subject = afterSales.Subject;
                    body = $"{subject}\n\nPedido: {order.Number}\n\n{afterSales.Description}\n\nInicia sesión para consultar los detalles:\n{settings.FrontendUrl}/account?tab=orders&order={order.Id}\n\nEste aviso corresponde a un evento registrado; el estado actual puede haber avanzado.";
                }
            }
            mail.ProviderMessageId = await postmark.SendAsync(to, subject, body, mail.Id, ct);
            mail.Status = "Sent";
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            mail.Status = mail.Attempts >= 5 ? "Failed" : "Pending";
            mail.NextAttemptUtc = now.AddMinutes(Math.Min(60, Math.Pow(2, mail.Attempts)));
        }
        finally
        {
            if (mail.Status is "Sent" or "Expired" or "Skipped" or "Failed") mail.ProtectedToken = null;
            mail.LeaseUntilUtc = null;
            await db.SaveChangesAsync(ct);
        }
    }
}

public class EmailDeliveryWorker(IServiceScopeFactory scopes, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (!scope.ServiceProvider.GetRequiredService<EmailSettings>().Ready) continue;
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>(); var now = DateTime.UtcNow;
                var ids = await db.EmailOutbox.AsNoTracking().Where(m => m.Status == "Pending" && m.NextAttemptUtc <= now && (m.LeaseUntilUtc == null || m.LeaseUntilUtc < now))
                    .OrderBy(m => m.CreatedAtUtc).Select(m => m.Id).Take(50).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    using var delivery = scopes.CreateScope();
                    await delivery.ServiceProvider.GetRequiredService<EmailDeliveryService>().ProcessAsync(id, stoppingToken);
                }
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("El procesamiento de correos no está disponible temporalmente."); }
        }
    }
}
