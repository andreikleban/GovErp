using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 3. For grant lines: the grant is active, the service date is within its period, and the cost is allowable under it.
/// </summary>
public sealed class GrantEligibleRule : ValidationRule
{
    private const string GrantNotActive = "GRANT_ELIGIBLE.GRANT_NOT_ACTIVE";
    private const string OutsidePeriod = "GRANT_ELIGIBLE.OUTSIDE_PERIOD";
    private const string DepartmentNotCovered = "GRANT_ELIGIBLE.DEPARTMENT_NOT_COVERED";
    private const string ObjectNotAllowable = "GRANT_ELIGIBLE.OBJECT_NOT_ALLOWABLE";

    public override string RuleId => "GRANT_ELIGIBLE";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every line with a grant segment, on the service date (the invoice date when there is none).
        var date = subject.Transaction.ServiceDate ?? subject.Transaction.Date;
        foreach (var line in subject.Distributions.Where(d => d.Account.Grant is not null))
        {
            var grant = line.KnownGrant();

            // Decide: the first reason the cost is not allowable under the grant.
            Verdict verdict;
            if (grant.Eligibility == GrantEligibilityResult.GrantNotActive)
                verdict = Fail(GrantNotActive);
            else if (grant.Eligibility == GrantEligibilityResult.OutsidePeriod)
                verdict = Fail(OutsidePeriod);
            else if (grant.Eligibility == GrantEligibilityResult.DepartmentNotAllowed)
                verdict = Fail(DepartmentNotCovered);
            else if (grant.Eligibility == GrantEligibilityResult.ObjectNotAllowed)
                verdict = Fail(ObjectNotAllowable);
            else
                continue;

            // Evidence
            yield return verdict.On(line)
                .InputAs("grant", grant.Code)
                .InputAs("grantStatus", grant.Status)
                .InputAs("department", line.Account.Department)
                .InputAs("object", line.Account.Object)
                .Input(date)
                .Computed(grant.Eligibility);
        }
    }
}
