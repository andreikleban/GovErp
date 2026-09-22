using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Шаг 1. Fund/Dept/Object гарантированы типом AccountCode; проверяется Grant по политике фонда.</summary>
public sealed class SegRequiredRule : IValidationRule
{
    public string RuleId => "SEG_REQUIRED";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => d.Fund is null || d.Fund.GrantRule == GrantRule.Required && d.Account.Grant is null)
            .Select(d => d.Fund is null ? RuleSupport.MissingFact(definition, d, "fund") : RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("fund", d.Fund!.Code), ("grantPolicy", d.Fund.GrantRule.ToString())),
                RuleSupport.Map(("missingSegment", "Grant")),
                $"Fund {d.Fund.Code} ({d.Fund.Name}) requires a grant segment on line {d.LineNo}."))
            .ToList();
}

