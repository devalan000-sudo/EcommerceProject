using Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class PaymentReconciliationWorker(IServiceScopeFactory scopes, ILogger<PaymentReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (!scope.ServiceProvider.GetRequiredService<StripeGateway>().Enabled) continue;
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                var ids = await db.Orders.AsNoTracking().Where(o => o.StripeHold).OrderBy(o => o.PaymentDeadlineUtc).Select(o => o.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        using var paymentScope = scopes.CreateScope();
                        await paymentScope.ServiceProvider.GetRequiredService<PaymentService>().ReconcileAsync(id, stoppingToken);
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("No se pudo conciliar un pago de prueba. Se conserva la reserva y se reintentará."); }
                }
                var refunds = await db.Orders.AsNoTracking().Where(o => o.PaymentMethod == Catalog.Api.Entities.PaymentMethod.Card && o.RefundStatus == "Pending")
                    .OrderBy(o => o.RefundRequestedAtUtc).Select(o => o.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var id in refunds)
                {
                    try
                    {
                        using var refundScope = scopes.CreateScope();
                        await refundScope.ServiceProvider.GetRequiredService<OrderResolutionService>().ReconcileRefundAsync(id, stoppingToken);
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("No se pudo conciliar un reembolso de prueba. Revisa los reembolsos pendientes en administración."); }
                }
                var partials = await db.ReturnRefunds.AsNoTracking().Where(r => r.ReturnRequest.Order.PaymentMethod == Catalog.Api.Entities.PaymentMethod.Card &&
                    (r.Status == Catalog.Api.Entities.ReturnRefundStatus.Pending || (r.Status == Catalog.Api.Entities.ReturnRefundStatus.NeedsReview && r.StripeRefundId != null)))
                    .OrderBy(r => r.CreatedAtUtc).Select(r => r.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var id in partials)
                {
                    try
                    {
                        using var partialScope = scopes.CreateScope();
                        await partialScope.ServiceProvider.GetRequiredService<ReturnRefundService>().ReconcileAsync(id, null, stoppingToken);
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("No se pudo conciliar un reembolso parcial de prueba. Se conserva su saldo pendiente."); }
                }
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Conciliación Stripe no disponible temporalmente."); }
        }
    }
}
