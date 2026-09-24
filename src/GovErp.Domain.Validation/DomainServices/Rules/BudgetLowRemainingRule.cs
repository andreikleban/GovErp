using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Step 5. An early warning: after the invoice, less than the configured share of the amended budget remains.</summary>
public sealed class BudgetLowRemainingRule : ValidationRule
{
    public override string RuleId => "BUDGET_LOW_REMAINING";

    protected override Severity DefaultSeverity => Severity.Warning;

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every budget line (account and fiscal year) the invoice charges.
        var threshold = parameters.Share("pct");
        foreach (var demand in BudgetAllocation.ByBudgetLine(subject))
        {
            var budget = demand.Budget;

            // Decide: little budget remains. An overage or a missing budget line is BUDGET_AVAILABILITY's finding.
            Verdict verdict;
            if (demand.Error is { } error)
                verdict = HardStop(error);
            else if (budget.Exists && budget.Amended > Money.Zero && !demand.AvailableAfter.IsNegative && demand.RemainingShare < threshold)
                verdict = Fail($"After this invoice, {demand.AvailableAfter} ({Percent(demand.RemainingShare)}%) remains for {demand.Account}.");
            else
                continue;

            // Evidence
            yield return verdict.OnGroup(demand.FirstLine)
                .Input(demand.Account)
                .Input(budget.Amended)
                .Input(budget.Available)
                .Input(budget.OwnHeld)
                .InputAs("thresholdPct", Percent(threshold))
                .Computed(demand.AvailableAfter)
                .ComputedAs("projectedAvailable", demand.AvailableAfter)
                .ComputedAs("remainingPct", Percent(demand.RemainingShare));
        }
    }
}
