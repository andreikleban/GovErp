using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class SegGrantForbiddenRule : IValidationRule
{
    public string RuleId => "SEG_GRANT_FORBIDDEN";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => d.Fund is null || d.Fund.GrantRule == GrantRule.Forbidden && d.Account.Grant is not null)
            .Select(d => d.Fund is null ? RuleSupport.MissingFact(definition, d, "fund") : RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("fund", d.Fund!.Code), ("grantPolicy", "Forbidden"), ("grant", d.Account.Grant!.Value)),
                RuleSupport.Map(),
                $"Fund {d.Fund.Code} ({d.Fund.Name}) does not accept a grant segment; line {d.LineNo} carries {d.Account.Grant}."))
            .ToList();
}

