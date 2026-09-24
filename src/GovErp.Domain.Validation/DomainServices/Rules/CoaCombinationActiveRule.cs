using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 2. The full account combination exists in the chart of accounts and is active on the invoice date.
/// </summary>
public sealed class CoaCombinationActiveRule : ValidationRule
{
    private const string CombinationUnknown = "COA_COMBINATION_ACTIVE.COMBINATION_UNKNOWN";
    private const string CombinationInactive = "COA_COMBINATION_ACTIVE.COMBINATION_INACTIVE";

    public override string RuleId => "COA_COMBINATION_ACTIVE";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: every invoice line, on the invoice date.
        var date = subject.Transaction.Date;
        foreach (var line in subject.Distributions)
        {
            var combination = line.Combination;

            // Decide: the combination is unknown, or known but not active on that date.
            Verdict verdict;
            if (!combination.Exists)
                verdict = Fail(CombinationUnknown);
            else if (!combination.IsActiveOnDate)
                verdict = Fail(CombinationInactive);
            else
                continue;

            // Evidence
            yield return verdict.On(line)
                .Input(combination.Exists)
                .Input(combination.Status)
                .Input(date);
        }
    }
}
