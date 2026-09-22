using System.Globalization;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class BudgetLowRemainingRule : IValidationRule
{
    public string RuleId => "BUDGET_LOW_REMAINING";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var threshold = definition.DecimalParameter("pct");
        if (threshold is < 0 or > 1)
            throw new GovErp.Domain.Validation.Exceptions.ValidationException("Low remaining pct must be between 0 and 1 inclusive.");
        var outcomes = new List<RuleOutcome>();
        foreach (var group in BudgetAllocation.Allocate(subject).GroupBy(x => (x.Distribution.Account, x.Distribution.Budget.FiscalYear)))
        {
            var d = group.First().Distribution;
            var b = d.Budget;
            var inputs = new Dictionary<string, string> { ["account"] = d.Account.ToString(),
                ["amended"] = b.Amended.ToString(), ["available"] = b.Available.ToString(),
                ["ownHeld"] = b.OwnHeld.ToString(), ["thresholdPct"] = (threshold * 100).ToString("0.00", CultureInfo.InvariantCulture) };
            var error = group.Select(x => x.Error).FirstOrDefault(x => x != null);
            if (error != null)
            {
                outcomes.Add(RuleOutcome.From(definition, Severity.HardStop, d.LineNo, inputs, new Dictionary<string, string>(), error));
                continue;
            }
            if (!b.Exists || b.Amended <= Money.Zero) continue;
            var after = b.AvailableForThisInvoice - new Money(group.Sum(x => x.RequiredNewBudget.Amount));
            if (after.IsNegative) continue;
            var pct = after.Amount / b.Amended.Amount;
            if (pct >= threshold) continue;
            var computed = new Dictionary<string, string> { ["availableAfter"] = after.ToString(),
                ["projectedAvailable"] = after.ToString(), ["remainingPct"] = (pct * 100).ToString("0.00", CultureInfo.InvariantCulture) };
            outcomes.Add(RuleOutcome.From(definition, definition.Severity ?? Severity.Warning, d.LineNo, inputs, computed,
                $"After this invoice, {after} ({computed["remainingPct"]}%) remains for {d.Account}."));
        }
        return outcomes;
    }
}

