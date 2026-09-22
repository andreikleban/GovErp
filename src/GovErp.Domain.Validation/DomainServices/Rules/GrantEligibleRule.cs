using System.Globalization;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class GrantEligibleRule : IValidationRule
{
    public string RuleId => "GRANT_ELIGIBLE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var date = (subject.Transaction.ServiceDate ?? subject.Transaction.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return subject.Distributions
            .Where(d => d.Account.Grant is not null && (d.Grant is null || d.Grant.Code != d.Account.Grant.Value || d.Grant.Eligibility != GrantEligibilityResult.Eligible))
            .Select(d => d.Grant is null ? RuleSupport.MissingFact(definition, d, "grant") :
                d.Grant.Code != d.Account.Grant!.Value ? RuleOutcome.From(definition, Severity.HardStop, d.LineNo,
                    RuleSupport.Inputs(d, ("grant", d.Grant.Code)), RuleSupport.Map(("mismatchedFact", "grant")),
                    $"Grant facts for {d.Grant.Code} do not match account grant {d.Account.Grant} (line {d.LineNo}).") :
                RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("grant", d.Grant.Code), ("grantStatus", d.Grant.Status), ("date", date)),
                RuleSupport.Map(("eligibility", d.Grant.Eligibility.ToString())),
                d.Grant.Eligibility switch
                {
                    GrantEligibilityResult.GrantNotActive => $"Grant {d.Grant.Code} is {d.Grant.Status} (line {d.LineNo}).",
                    GrantEligibilityResult.OutsidePeriod => $"Service date (or invoice date when absent) {date} is outside the period of grant {d.Grant.Code} (line {d.LineNo}).",
                    GrantEligibilityResult.DepartmentNotAllowed => $"Department {d.Account.Department} is not covered by grant {d.Grant.Code} (line {d.LineNo}).",
                    _ => $"Object {d.Account.Object} is not an allowable cost under grant {d.Grant.Code} (line {d.LineNo}).",
                }))
            .ToList();
    }
}

