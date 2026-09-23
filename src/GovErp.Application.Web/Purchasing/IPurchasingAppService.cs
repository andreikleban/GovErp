using GovErp.Application.Web.Common;
using GovErp.Application.Web.Purchasing.Contracts;

namespace GovErp.Application.Web.Purchasing;

public interface IPurchasingAppService
{
    Task<IReadOnlyList<PurchaseOrderListItemVm>> ListOrdersAsync(ActorContext actor, CancellationToken ct = default);
    /// <summary>Карточка заказа по номеру. NotFound — неизвестный номер.</summary>
    Task<PurchaseOrderDetailVm> GetOrderAsync(string number, ActorContext actor, CancellationToken ct = default);
}
