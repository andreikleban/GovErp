namespace GovErp.Application.Web.Purchasing.Contracts;

/// <summary>Карточка заказа: строки, каждая — со своим encumbrance и claims (Purchasing › Purchase Orders).</summary>
public sealed record PurchaseOrderDetailVm(string Number, Guid VendorId, string VendorName, string Status, decimal Total,
    IReadOnlyList<PurchaseOrderLineDetailVm> Lines);
