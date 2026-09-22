using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class BudgetAvailabilityRule : IValidationRule
{
    public string RuleId => "BUDGET_AVAILABILITY";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var outcomes = new List<RuleOutcome>();
        foreach (var group in BudgetAllocation.Allocate(subject).GroupBy(x => (x.Distribution.Account, x.Distribution.Budget.FiscalYear)))
        {
            var d = group.First().Distribution;
            var b = d.Budget;
            var required = new Money(group.Sum(x => x.RequiredNewBudget.Amount));
            var liquidation = new Money(group.Sum(x => x.LiquidationAmount.Amount));
            var after = b.Available + b.OwnHeld - required;
            var inputs = new Dictionary<string, string> {
                ["account"] = d.Account.ToString(), ["fiscalYear"] = b.FiscalYear.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["amended"] = b.Amended.ToString(), ["actuals"] = b.Actuals.ToString(),
                ["encumbered"] = b.Encumbered.ToString(), ["held"] = b.Held.ToString(),
                ["ownHeld"] = b.OwnHeld.ToString(), ["amountToCheck"] = required.ToString(),
                ["invoiceAmount"] = new Money(group.Sum(x => x.Distribution.Amount.Amount)).ToString(),
                ["controlMode"] = d.Fund?.Control.ToString() ?? "Unknown" };
            var computed = new Dictionary<string, string> {
                ["available"] = b.Available.ToString(), ["availableForThisInvoice"] = b.AvailableForThisInvoice.ToString(),
                ["requiredNewBudget"] = required.ToString(), ["eligibleLiquidation"] = liquidation.ToString(),
                ["overage"] = Money.Max(Money.Zero, -after).ToString(), ["availableAfter"] = after.ToString(),
                ["projectedAvailable"] = after.ToString() };
            var error = group.Select(x => x.Error).FirstOrDefault(x => x != null);
            if (error != null || !b.Exists)
                outcomes.Add(RuleOutcome.From(definition, Severity.HardStop, d.LineNo, inputs, computed,
                    error ?? $"No budget line exists for {d.Account} in FY{b.FiscalYear}."));
            else if (after.IsNegative)
                outcomes.Add(RuleOutcome.From(definition, d.Fund?.Control == BudgetControl.Soft ? Severity.SoftStop : Severity.HardStop,
                    d.LineNo, inputs, computed, $"Invoice exceeds available budget for {d.Account} in FY{b.FiscalYear} by {-after}."));
        }
        return outcomes;
    }
}
