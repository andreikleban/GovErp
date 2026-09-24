using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 3. The fund is active and the line's department and object are an allowed use of it.
/// </summary>
public sealed class FundDeptObjectAllowedRule : ValidationRule
{
    private const string FundInactive = "FUND_DEPT_OBJECT_ALLOWED.FUND_INACTIVE";
    private const string DepartmentNotAllowed = "FUND_DEPT_OBJECT_ALLOWED.DEPARTMENT_NOT_ALLOWED";
    private const string ObjectNotAllowed = "FUND_DEPT_OBJECT_ALLOWED.OBJECT_NOT_ALLOWED";

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
                verdict = Fail(FundInactive);
            else if (fund.Restriction == FundRestriction.DepartmentNotAllowed)
                verdict = Fail(DepartmentNotAllowed);
            else if (fund.Restriction == FundRestriction.ObjectNotAllowed)
                verdict = Fail(ObjectNotAllowed);
            else
                continue;

            // Evidence
            yield return verdict.On(line)
                .InputAs("fund", fund.Code)
                .InputAs("fundName", fund.Name)
                .InputAs("department", line.Account.Department)
                .InputAs("object", line.Account.Object)
                .Computed(fund.Restriction)
                .ComputedAs("active", fund.IsActive);
        }
    }
}
