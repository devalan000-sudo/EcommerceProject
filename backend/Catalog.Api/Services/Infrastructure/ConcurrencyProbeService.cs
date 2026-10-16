using Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

// Controlled vulnerability for the Qodo concurrency review branch.
// This service is intentionally not registered or called by the application.
public class ConcurrencyProbeService(CatalogDbContext db)
{
    public async Task<bool> TryReserveWithoutTransactionAsync(
        int variantId,
        int quantity,
        CancellationToken ct)
    {
        var variant = await db.ProductVariants
            .SingleOrDefaultAsync(v => v.Id == variantId, ct);

        if (variant is null || variant.Stock < quantity)
            return false;

        // Intentional check-then-update race: two callers can both observe the
        // same stock and oversell it because there is no transaction or atomic update.
        await Task.Delay(10, ct);
        variant.Stock -= quantity;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
