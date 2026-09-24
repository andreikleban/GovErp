using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 5. Per budget line: available = amended − actuals − encumbered − held. The invoice needs new budget for its amount
/// minus what it liquidates from a PO, and its own reservation is added back. The fund's budget control sets the severity.
/// </summary>
public sealed class BudgetAvailabilityRule : ValidationRule
{
    public override string RuleId => "BUDGET_AVAILABILITY";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every budget line (account and fiscal year) the invoice charges.
        foreach (var demand in BudgetAllocation.ByBudgetLine(subject))
        {
            var budget = demand.Budget;

            // Decide: inconsistent balances, no budget line, or not enough budget (hard or soft by the fund's control).
            Verdict verdict;
            if (demand.Error is { } error)
                verdict = HardStop(error);
            else if (!budget.Exists)
                verdict = HardStop($"No budget line exists for {demand.Account} in FY{budget.FiscalYear}.");
            else if (demand.AvailableAfter.IsNegative && demand.Control == BudgetControl.Soft)
                verdict = SoftStop($"Invoice exceeds available budget for {demand.Account} in FY{budget.FiscalYear} by {demand.Overage}.");
            else if (demand.AvailableAfter.IsNegative)
                verdict = HardStop($"Invoice exceeds available budget for {demand.Account} in FY{budget.FiscalYear} by {demand.Overage}.");
            else
                continue;

            // Evidence
            yield return verdict.OnGroup(demand.FirstLine)
                .Input(demand.Account)
                .Input(budget.FiscalYear)
                .Input(budget.Amended)
                .Input(budget.Actuals)
                .Input(budget.Encumbered)
                .Input(budget.Held)
                .Input(budget.OwnHeld)
                .InputAs("amountToCheck", demand.RequiredNewBudget)
                .Input(demand.InvoiceAmount)
                .InputAs("controlMode", demand.Control?.ToString() ?? "Unknown")
                .Computed(budget.Available)
                .Computed(budget.AvailableForThisInvoice)
                .Computed(demand.RequiredNewBudget)
                .ComputedAs("eligibleLiquidation", demand.Liquidation)
                .Computed(demand.Overage)
                .Computed(demand.AvailableAfter)
                .ComputedAs("projectedAvailable", demand.AvailableAfter);
        }
    }
}
