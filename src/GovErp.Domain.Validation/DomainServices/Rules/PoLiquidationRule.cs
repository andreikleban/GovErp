using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 6. An invoice against a PO line first liquidates the line's encumbrance; the rest needs new budget
/// (checked by BUDGET_AVAILABILITY). Cumulative billing above the authorized amount is limited by a tolerance.
/// </summary>
public sealed class PoLiquidationRule : ValidationRule
{
    public override string RuleId => "PO_LIQUIDATION";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every PO line the invoice bills.
        var tolerance = parameters.Share("tolerance_pct");
        foreach (var billing in BudgetAllocation.ByPoLine(subject))
        {
            var po = billing.Po;
            var liquidates = $"Invoice liquidates {billing.Liquidation} of {billing.Ref}; {billing.NeedsNewBudget} requires new budget.";

            // Decide: strictest case first. Every billed PO line is recorded, even when fully covered.
            Verdict verdict;
            if (billing.Error is { } error)
                verdict = HardStop(error);
            else if (billing.ExceedsTolerance(tolerance))
                verdict = HardStop($"Cumulative billing for {billing.Ref} exceeds its authorized tolerance; a change order is required.");
            else if (billing.NeedsNewBudget > Money.Zero || billing.CumulativeExcess > Money.Zero)
                verdict = Warning(liquidates);
            else
                verdict = Allowed(liquidates);

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
