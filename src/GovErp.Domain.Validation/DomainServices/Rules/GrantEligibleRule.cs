using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Step 3. For grant lines: the grant is active, the service date is within its period, and the cost is allowable under it.</summary>
public sealed class GrantEligibleRule : ValidationRule
{
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
                verdict = Fail($"Grant {grant.Code} is {grant.Status} (line {line.LineNo}).");
            else if (grant.Eligibility == GrantEligibilityResult.OutsidePeriod)
                verdict = Fail($"Service date (or invoice date when absent) {date:yyyy-MM-dd} is outside the period of grant {grant.Code} (line {line.LineNo}).");
            else if (grant.Eligibility == GrantEligibilityResult.DepartmentNotAllowed)
                verdict = Fail($"Department {line.Account.Department} is not covered by grant {grant.Code} (line {line.LineNo}).");
            else if (grant.Eligibility == GrantEligibilityResult.ObjectNotAllowed)
                verdict = Fail($"Object {line.Account.Object} is not an allowable cost under grant {grant.Code} (line {line.LineNo}).");
            else
                continue;

            // Evidence
            yield return verdict.On(line)
                .InputAs("grant", grant.Code)
                .InputAs("grantStatus", grant.Status)
                .Input(date)
                .Computed(grant.Eligibility);
        }
    }
}
