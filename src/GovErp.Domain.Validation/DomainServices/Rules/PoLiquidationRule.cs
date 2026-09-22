using System.Globalization;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class PoLiquidationRule : IValidationRule
{
    public string RuleId => "PO_LIQUIDATION";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var tolerance = definition.DecimalParameter("tolerance_pct");
        if (tolerance is < 0 or > 1)
            throw new GovErp.Domain.Validation.Exceptions.ValidationException("PO tolerance_pct must be between 0 and 1 inclusive.");
        var outcomes = new List<RuleOutcome>();
        var allocation = BudgetAllocation.Allocate(subject);
        foreach (var missing in allocation.Where(x => x.Distribution.Encumbrance == null && subject.Transaction.IsPoBacked))
            outcomes.Add(RuleOutcome.From(definition, Severity.HardStop, missing.Distribution.LineNo,
                new Dictionary<string, string>(), new Dictionary<string, string>(), missing.Error ?? "Missing PO line snapshot."));
        foreach (var group in allocation.Where(x => x.Distribution.Encumbrance != null).GroupBy(x => x.Distribution.Encumbrance!.PoLineRef))
        {
            var d = group.First().Distribution;
            var po = d.Encumbrance!;
            var amount = new Money(group.Sum(x => x.Distribution.Amount.Amount));
            var liquidation = new Money(group.Sum(x => x.LiquidationAmount.Amount));
            var projected = po.AlreadyPostedAgainstPo + po.OtherActiveInvoiceClaims + amount;
            var cumulativeExcess = Money.Max(Money.Zero, projected - po.AuthorizedPoAmount);
            var pct = po.AuthorizedPoAmount > Money.Zero ? cumulativeExcess.Amount / po.AuthorizedPoAmount.Amount : 0m;
            var inputs = new Dictionary<string, string> { ["poLine"] = po.PoLineRef, ["remaining"] = po.Remaining.ToString(),
                ["authorizedPoAmount"] = po.AuthorizedPoAmount.ToString(), ["alreadyPostedAgainstPo"] = po.AlreadyPostedAgainstPo.ToString(),
                ["otherActiveInvoiceClaims"] = po.OtherActiveInvoiceClaims.ToString(), ["currentInvoicePoAmount"] = amount.ToString(),
                ["otherLiquidationClaims"] = po.OtherLiquidationClaims.ToString(), ["ownLiquidationClaim"] = po.OwnLiquidationClaim.ToString(),
                ["tolerancePct"] = (tolerance * 100).ToString("0.00", CultureInfo.InvariantCulture) };
            var computed = new Dictionary<string, string> { ["liquidation"] = liquidation.ToString(), ["excess"] = (amount - liquidation).ToString(),
                ["projectedBilled"] = projected.ToString(), ["cumulativeExcess"] = cumulativeExcess.ToString(),
                ["excessPct"] = (pct * 100).ToString("0.00", CultureInfo.InvariantCulture) };
            var error = group.Select(x => x.Error).FirstOrDefault(x => x != null);
            var exceeds = cumulativeExcess.Amount > po.AuthorizedPoAmount.Amount * tolerance;
            var severity = error != null || exceeds ? Severity.HardStop
                : amount > liquidation || cumulativeExcess > Money.Zero ? Severity.Warning : Severity.Allowed;
            outcomes.Add(RuleOutcome.From(definition, severity, d.LineNo, inputs, computed,
                error ?? (exceeds ? $"Cumulative billing for {po.PoLineRef} exceeds its authorized tolerance; a change order is required."
                    : $"Invoice liquidates {liquidation} of {po.PoLineRef}; {amount - liquidation} requires new budget.")));
        }
        return outcomes;
    }
}

