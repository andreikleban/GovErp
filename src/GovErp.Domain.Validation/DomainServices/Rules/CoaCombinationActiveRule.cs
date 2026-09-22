using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class CoaCombinationActiveRule : IValidationRule
{
    public string RuleId => "COA_COMBINATION_ACTIVE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => d.Combination is null || !d.Combination.Exists || !d.Combination.IsActiveOnDate)
            .Select(d => d.Combination is null ? RuleSupport.MissingFact(definition, d, "combination") : RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("exists", d.Combination.Exists.ToString()), ("status", d.Combination.Status), ("date", subject.Transaction.Date.ToString("yyyy-MM-dd"))),
                RuleSupport.Map(),
                d.Combination.Exists
                    ? $"Account combination {d.Account} is {d.Combination.Status} on {subject.Transaction.Date:yyyy-MM-dd} (line {d.LineNo})."
                    : $"Account combination {d.Account} does not exist in the chart of accounts (line {d.LineNo})."))
            .ToList();
}

