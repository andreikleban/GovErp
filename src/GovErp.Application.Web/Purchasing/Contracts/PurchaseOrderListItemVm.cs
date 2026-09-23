namespace GovErp.Application.Web.Purchasing.Contracts;

/// <summary>Строка списка Purchasing › Purchase Orders. Remaining — сумма Remaining всех encumbrance строк заказа.</summary>
public sealed record PurchaseOrderListItemVm(string Number, string VendorName, string Status, decimal Total, decimal Remaining);
