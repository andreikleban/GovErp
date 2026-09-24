using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Step 3. The fund is active and the line's department and object are an allowed use of it.</summary>
public sealed class FundDeptObjectAllowedRule : ValidationRule
{
    public override string RuleId => "FUND_DEPT_OBJECT_ALLOWED";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every invoice line.
        foreach (var line in subject.Distributions)
        {
            var fund = line.KnownFund();

            // Decide: an inactive fund, or a department or object the fund does not allow.
            Verdict verdict;
            if (!fund.IsActive)
                verdict = Fail($"Fund {fund.Code} is inactive (line {line.LineNo}).");
            else if (fund.Restriction == FundRestriction.DepartmentNotAllowed)
                verdict = Fail($"Department {line.Account.Department} may not spend from fund {fund.Code} ({fund.Name}) (line {line.LineNo}).");
            else if (fund.Restriction == FundRestriction.ObjectNotAllowed)
                verdict = Fail($"Object {line.Account.Object} is not an allowed use of fund {fund.Code} ({fund.Name}) (line {line.LineNo}).");
            else
                continue;

            // Evidence
            yield return verdict.On(line)
                .InputAs("fund", fund.Code)
                .Computed(fund.Restriction)
                .ComputedAs("active", fund.IsActive);
        }
    }
}
