using GovErp.Domain.Payables.Entities;
namespace GovErp.Domain.Payables.Repositories;

/// <summary>
/// Collection of purchase orders.
/// </summary>
public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> FindByNumberAsync(string number, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken ct = default);
}
