using GovErp.Application.Web.Common;
using GovErp.Application.Web.Purchasing.Contracts;

namespace GovErp.Application.Web.Purchasing;

public interface IPurchasingAppService
{
    Task<IReadOnlyList<PurchaseOrderListItemVm>> ListOrdersAsync(ActorContext actor, CancellationToken ct = default);
    /// <summary>Purchase order card by number. NotFound for an unknown number.</summary>
    Task<PurchaseOrderDetailVm> GetOrderAsync(string number, ActorContext actor, CancellationToken ct = default);
}
