namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// A purchase-order line on screen, including the encumbrance still remaining.
/// </summary>
public sealed record PurchaseOrderLineVm(int LineNo, string Account, decimal Amount, decimal AuthorizedPoAmount, decimal AlreadyPostedAgainstPo, decimal RemainingEncumbrance);

/// <summary>
/// A purchase order as shown on screen.
/// </summary>
public sealed record PurchaseOrderVm(string Number, Guid VendorId, string Status, IReadOnlyList<PurchaseOrderLineVm> Lines);
