using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.RuleSupport;

/// <summary>How the invoice bills one PO line: what it liquidates, what needs new budget, how far cumulative billing goes past the PO.</summary>
public sealed record PoLineBilling(DistributionSnapshot FirstLine, EncumbranceSnapshot Po, Money InvoiceAmount, Money Liquidation,
    string? Error)
{
    public string Ref => Po.PoLineRef;

    public Money NeedsNewBudget => InvoiceAmount - Liquidation;

    /// <summary>Already posted + other open invoices + this invoice.</summary>
    public Money ProjectedBilled => Po.AlreadyPostedAgainstPo + Po.OtherActiveInvoiceClaims + InvoiceAmount;

    public Money CumulativeExcess => Money.Max(Money.Zero, ProjectedBilled - Po.AuthorizedPoAmount);

    public decimal ExcessShare => Po.AuthorizedPoAmount > Money.Zero ? CumulativeExcess.Amount / Po.AuthorizedPoAmount.Amount : 0m;

    public bool ExceedsTolerance(decimal tolerance) => CumulativeExcess.Amount > Po.AuthorizedPoAmount.Amount * tolerance;
}
