using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 1. A fund whose grant policy is Required needs a grant segment on every line charged to it.
/// </summary>
public sealed class SegRequiredRule : ValidationRule
{
    private const string GrantSegmentMissing = "SEG_REQUIRED.GRANT_SEGMENT_MISSING";

    public override string RuleId => "SEG_REQUIRED";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every invoice line. Fund, department and object are guaranteed by the AccountCode type.
        foreach (var line in subject.Distributions)
        {
            var fund = line.KnownFund();

            // Decide: the fund requires a grant segment and the line has none.
            Verdict verdict;
            if (fund.GrantRule == GrantRule.Required && line.Account.Grant is null)
                verdict = Fail(GrantSegmentMissing);
            else
                continue;

            // Evidence
            yield return verdict.On(line)
                .InputAs("fund", fund.Code)
                .InputAs("fundName", fund.Name)
                .InputAs("grantPolicy", fund.GrantRule)
                .ComputedAs("missingSegment", "Grant");
        }
    }
}
