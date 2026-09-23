namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>
/// Encumbrance строки PO: суммы и claims ликвидации/billing с номерами инвойсов. Используется и в Budget › Encumbrances,
/// и в карточке строки бюджета (encumbrance по счёту), и в карточке заказа (Purchasing).
/// </summary>
public sealed record EncumbranceVm(string PoLineRef, string Account, string Status, decimal Original, decimal Liquidated, decimal Released,
    decimal Remaining, decimal AuthorizedPoAmount, decimal AlreadyPostedAgainstPo,
    IReadOnlyList<EncumbranceClaimVm> Claims, IReadOnlyList<EncumbranceClaimVm> BillingClaims);
