namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>
/// Encumbrance of a PO line: amounts and liquidation/billing claims with invoice numbers. Used in Budget › Encumbrances,
/// in the budget line card (encumbrances on the account) and in the purchase order card (Purchasing).
/// </summary>
public sealed record EncumbranceVm(string PoLineRef, string Account, string Status, decimal Original, decimal Liquidated, decimal Released,
    decimal Remaining, decimal AuthorizedPoAmount, decimal AlreadyPostedAgainstPo,
    IReadOnlyList<EncumbranceClaimVm> Claims, IReadOnlyList<EncumbranceClaimVm> BillingClaims);
