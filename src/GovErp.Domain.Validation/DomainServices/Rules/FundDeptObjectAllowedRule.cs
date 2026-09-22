using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class FundDeptObjectAllowedRule : IValidationRule
{
    public string RuleId => "FUND_DEPT_OBJECT_ALLOWED";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => d.Fund is null || d.Fund.Code != d.Account.Fund.Value || !d.Fund.IsActive || d.Fund.Restriction != FundRestriction.Allowed)
            .Select(d => d.Fund is null ? RuleSupport.MissingFact(definition, d, "fund") :
                d.Fund.Code != d.Account.Fund.Value ? RuleOutcome.From(definition, Severity.HardStop, d.LineNo,
                    RuleSupport.Inputs(d, ("fund", d.Fund.Code)), RuleSupport.Map(("mismatchedFact", "fund")),
                    $"Fund facts for {d.Fund.Code} do not match account fund {d.Account.Fund} (line {d.LineNo}).") :
                RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("fund", d.Fund!.Code)),
                RuleSupport.Map(("restriction", d.Fund.Restriction.ToString()), ("active", d.Fund.IsActive.ToString())),
                !d.Fund.IsActive ? $"Fund {d.Fund.Code} is inactive (line {d.LineNo})." :
                d.Fund.Restriction == FundRestriction.DepartmentNotAllowed
                    ? $"Department {d.Account.Department} may not spend from fund {d.Fund.Code} ({d.Fund.Name}) (line {d.LineNo})."
                    : $"Object {d.Account.Object} is not an allowed use of fund {d.Fund.Code} ({d.Fund.Name}) (line {d.LineNo})."))
            .ToList();
}

