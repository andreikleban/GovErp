namespace GovErp.Application.Web.Purchasing.Contracts;

/// <summary>
/// Purchase order card: lines, each with its encumbrance and claims (Purchasing › Purchase Orders).
/// </summary>
public sealed record PurchaseOrderDetailVm(string Number, Guid VendorId, string VendorName, string Status, decimal Total,
    IReadOnlyList<PurchaseOrderLineDetailVm> Lines);
