using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Step 2. The full account combination exists in the chart of accounts and is active on the invoice date.</summary>
public sealed class CoaCombinationActiveRule : ValidationRule
{
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
                verdict = Fail($"Account combination {line.Account} does not exist in the chart of accounts (line {line.LineNo}).");
            else if (!combination.IsActiveOnDate)
                verdict = Fail($"Account combination {line.Account} is {combination.Status} on {date:yyyy-MM-dd} (line {line.LineNo}).");
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
