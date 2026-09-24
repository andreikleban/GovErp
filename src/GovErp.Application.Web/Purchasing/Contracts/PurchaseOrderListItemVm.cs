namespace GovErp.Application.Web.Purchasing.Contracts;

/// <summary>
/// A row of Purchasing › Purchase Orders. Remaining is the sum of Remaining over the encumbrances of the order's lines.
/// </summary>
public sealed record PurchaseOrderListItemVm(string Number, string VendorName, string Status, decimal Total, decimal Remaining);
