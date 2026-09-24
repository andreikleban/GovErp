using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL repository of purchase orders for the current tenant.
/// </summary>
public sealed class EfPurchaseOrderRepository(GovErpDbContext db) : IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> FindByNumberAsync(string number, CancellationToken ct = default) =>
        db.PurchaseOrders.SingleOrDefaultAsync(p => p.Number == number, ct);

    public async Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken ct = default) =>
        await db.PurchaseOrders.OrderBy(p => p.Number).ToListAsync(ct);
}
