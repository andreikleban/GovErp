using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Overall = строжайший outcome без снятых overrides. Override снимает только Soft Stop и только если он
/// привязан к тому же outcome предыдущей оценки (правило, версия, строка, доказательства) в том же цикле,
/// версии содержания и наборе правил, выдан уполномоченной ролью, не автором и с причиной.
/// </summary>
public static class OutcomeAggregation
{
    public static (IReadOnlyList<RuleOutcome> WithOverrides, Severity Overall) Apply(
        IReadOnlyList<RuleOutcome> outcomes, ValidationSubject subject, RuleSetVersions currentVersions)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(currentVersions);

        var result = outcomes.Select(outcome =>
        {
            if (outcome.Severity != Severity.SoftStop || outcome.OverridableBy.Count == 0)
            {
                return outcome;
            }

            var match = subject.OverridesSoFar.FirstOrDefault(o => Binds(o, outcome, subject, currentVersions));
            return match is null ? outcome : outcome with { OverriddenBy = match };
        }).ToArray();

        var overall = result.Where(o => !o.IsOverridden).Select(o => o.Severity).DefaultIfEmpty(Severity.Allowed).Max();
        return (Array.AsReadOnly(result), overall);
    }

    private static bool Binds(OverrideSnapshot o, RuleOutcome current, ValidationSubject subject, RuleSetVersions versions)
    {
        var transaction = subject.Transaction;
        if (o.RuleId != current.RuleId || o.RuleVersion != current.RuleVersion || o.DistributionLine != current.DistributionLine
            || o.ContentVersion != transaction.ContentVersion || o.ApprovalCycleId != transaction.ApprovalCycleId
            || string.IsNullOrWhiteSpace(versions.Fingerprint) || o.RuleFingerprint != versions.Fingerprint
            || o.EvaluationId == Guid.Empty || o.EvaluationId != subject.PreviousEvaluationId
            || o.Role is not { } role || !current.OverridableBy.Contains(role)
            || o.UserId.Value == Guid.Empty || o.UserId == transaction.CreatedBy
            || string.IsNullOrWhiteSpace(o.Reason))
        {
            return false;
        }

        var previous = subject.PreviousOutcomes.SingleOrDefault(p => p.OutcomeRef == o.OutcomeRef);
        return previous is not null
            && previous.Severity == Severity.SoftStop
            && previous.RuleId == current.RuleId && previous.RuleVersion == current.RuleVersion
            && previous.DistributionLine == current.DistributionLine
            && SameEvidence(previous.Inputs, current.Inputs);
    }

    /// <summary>Override выдан на конкретные цифры: если входы изменились, это новое исключение, а не старое.</summary>
    private static bool SameEvidence(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
