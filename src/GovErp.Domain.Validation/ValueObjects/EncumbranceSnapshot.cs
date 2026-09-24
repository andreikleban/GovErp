using GovErp.Domain.Validation.Codes;

namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Remaining amount and claims of a purchase-order line, for the rules.
/// </summary>
public sealed record EncumbranceSnapshot(string PoLineRef, Money Remaining, bool IsOpen,
    Money AuthorizedPoAmount = default, Money AlreadyPostedAgainstPo = default,
    Money OtherActiveInvoiceClaims = default, Money CurrentInvoicePoAmount = default,
    Money OtherLiquidationClaims = default, Money OwnLiquidationClaim = default)
{
    public Money ClaimableForInvoice => IsOpen ? Money.Max(Money.Zero, Remaining - OtherLiquidationClaims) : Money.Zero;
    public Money ProjectedBilled => AlreadyPostedAgainstPo + OtherActiveInvoiceClaims + CurrentInvoicePoAmount;
    public Money CumulativeExcess => Money.Max(Money.Zero, ProjectedBilled - AuthorizedPoAmount);
    public decimal CumulativePoExcessPct => AuthorizedPoAmount > Money.Zero
        ? CumulativeExcess.Amount / AuthorizedPoAmount.Amount
        : throw new InvalidOperationException(Problem.Of(AllocationErrors.PoLineInvalid).ToString());
}
