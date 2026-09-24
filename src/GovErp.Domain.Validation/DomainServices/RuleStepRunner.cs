using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Steps 1–6: runs the catalog rules step by step. Each rule sees only the lines of the scopes (fund, grant) where its
/// definition is in force. A Hard Stop in a step skips the remaining steps; a skipped step is recorded as skipped.
/// </summary>
internal sealed class RuleStepRunner
{
    private static readonly ValidationStep[] RuleSteps =
    [
        ValidationStep.RequiredSegments, ValidationStep.ValidCombination, ValidationStep.FundAndGrantRestrictions,
        ValidationStep.TransactionPurpose, ValidationStep.BudgetAvailability, ValidationStep.EncumbranceImpact,
    ];

    private readonly RuleCatalog _catalog;

    public RuleStepRunner(RuleCatalog catalog) => _catalog = catalog;

    public (IReadOnlyList<RuleOutcome> Outcomes, IReadOnlyList<StepExecution> Steps) Run(ValidationSubject subject, EffectiveRuleSet rules)
    {
        var assignments = Assign(subject, rules);
        var outcomes = new List<RuleOutcome>();
        var steps = new List<StepExecution>();
        foreach (var step in RuleSteps)
        {
            if (outcomes.Any(o => o.Severity == Severity.HardStop))
            {
                steps.Add(new(step, StepExecutionStatus.Skipped));
                continue;
            }

            foreach (var (definition, scoped) in assignments.Where(a => a.Definition.Step == step))
            {
                outcomes.AddRange(_catalog.Find(definition.RuleId)!.Evaluate(scoped, definition));
            }

            steps.Add(new(step, StepExecutionStatus.Executed));
        }

        return (outcomes, steps);
    }

    /// <summary>Each definition gets the lines of the scopes where it is in force, in a stable order.</summary>
    private static IReadOnlyList<(RuleDefinition Definition, ValidationSubject Scoped)> Assign(ValidationSubject subject, EffectiveRuleSet rules)
    {
        var lines = new Dictionary<RuleDefinition, List<DistributionSnapshot>>(ReferenceEqualityComparer.Instance);
        foreach (var scope in subject.Distributions.GroupBy(d => (Fund: (string?)d.Account.Fund.Value, Grant: d.Account.Grant?.Value)))
        {
            foreach (var definition in rules.ForScope(scope.Key.Fund, scope.Key.Grant).Rules)
            {
                if (!lines.TryGetValue(definition, out var list))
                {
                    lines[definition] = list = [];
                }

                list.AddRange(scope);
            }
        }

        return lines
            .OrderBy(pair => pair.Key.Step).ThenBy(pair => pair.Key.RuleId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Layer).ThenBy(pair => pair.Key.Version)
            .Select(pair => (pair.Key, subject with { Distributions = pair.Value.OrderBy(d => d.LineNo).ToArray() }))
            .ToArray();
    }
}
