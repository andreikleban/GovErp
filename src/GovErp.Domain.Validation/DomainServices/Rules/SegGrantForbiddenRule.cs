using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 1. A fund whose grant policy is Forbidden does not accept a grant segment.
/// </summary>
public sealed class SegGrantForbiddenRule : ValidationRule
{
    private const string GrantSegmentNotAccepted = "SEG_GRANT_FORBIDDEN.GRANT_SEGMENT_NOT_ACCEPTED";

    public override string RuleId => "SEG_GRANT_FORBIDDEN";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every invoice line.
        foreach (var line in subject.Distributions)
        {
            var fund = line.KnownFund();
            var grant = line.Account.Grant;

            // Decide: the line carries a grant segment the fund does not accept.
            Verdict verdict;
            if (fund.GrantRule == GrantRule.Forbidden && grant is not null)
                verdict = Fail(GrantSegmentNotAccepted);
            else
                continue;

            // Evidence
            yield return verdict.On(line)
                .InputAs("fund", fund.Code)
                .InputAs("fundName", fund.Name)
                .InputAs("grantPolicy", fund.GrantRule)
                .InputAs("grant", grant.Value);
        }
    }
}
