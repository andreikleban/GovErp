using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 6. An invoice against a PO line first liquidates the line's encumbrance; the rest needs new budget
/// (checked by BUDGET_AVAILABILITY). Cumulative billing above the authorized amount is limited by a tolerance.
/// </summary>
public sealed class PoLiquidationRule : ValidationRule
{
    private const string ExceedsTolerance = "PO_LIQUIDATION.EXCEEDS_TOLERANCE";
    private const string NeedsNewBudget = "PO_LIQUIDATION.NEEDS_NEW_BUDGET";
    private const string FullyLiquidated = "PO_LIQUIDATION.FULLY_LIQUIDATED";

    private static readonly ParameterSpec Tolerance = ParameterSpec.Share("tolerance_pct", Stricter.WhenLower);

    public override string RuleId => "PO_LIQUIDATION";

    public override IReadOnlyList<ParameterSpec> Parameters => [Tolerance];

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every PO line the invoice bills.
        var tolerance = parameters.Read(Tolerance);
        foreach (var billing in BudgetAllocation.ByPoLine(subject))
        {
            var po = billing.Po;

            // Decide: strictest case first. Every billed PO line is recorded, even when fully covered.
            Verdict verdict;
            if (billing.Error is { } error)
                verdict = HardStop(error);
            else if (billing.ExceedsTolerance(tolerance))
                verdict = HardStop(ExceedsTolerance);
            else if (billing.NeedsNewBudget > Money.Zero || billing.CumulativeExcess > Money.Zero)
                verdict = Warning(NeedsNewBudget);
            else
                verdict = Allowed(FullyLiquidated);

            // Evidence
            yield return verdict.OnGroup(billing.FirstLine)
                .InputAs("poLine", billing.Ref)
                .Input(po.Remaining)
                .Input(po.AuthorizedPoAmount)
                .Input(po.AlreadyPostedAgainstPo)
                .Input(po.OtherActiveInvoiceClaims)
                .InputAs("currentInvoicePoAmount", billing.InvoiceAmount)
                .Input(po.OtherLiquidationClaims)
                .Input(po.OwnLiquidationClaim)
                .InputAs("tolerancePct", Percent(tolerance))
                .Computed(billing.Liquidation)
                .ComputedAs("excess", billing.NeedsNewBudget)
                .Computed(billing.ProjectedBilled)
                .Computed(billing.CumulativeExcess)
                .ComputedAs("excessPct", Percent(billing.ExcessShare));
        }
    }
}
