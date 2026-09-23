using GovErp.Application.Web.Budget.Contracts;

namespace GovErp.Application.Web.Purchasing.Contracts;

/// <summary>Строка заказа с encumbrance этой строки (claims ликвидации и billing с номерами инвойсов).</summary>
public sealed record PurchaseOrderLineDetailVm(int LineNo, string Account, decimal Amount, EncumbranceVm? Encumbrance);
