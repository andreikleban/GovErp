using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Overall = the strictest outcome excluding released overrides. An override releases only a Soft Stop, and only if it
/// is bound to the same outcome of the previous evaluation (rule, version, line, evidence) in the same cycle,
/// content version and rule set, granted by an authorized role, not by the author, and with a reason.
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
            || o.EvaluationId == Guid.Empty
            || o.Role is not { } role || !current.OverridableBy.Contains(role)
            || o.UserId.Value == Guid.Empty || o.UserId == transaction.CreatedBy
            || string.IsNullOrWhiteSpace(o.Reason))
        {
            return false;
        }

        var evidence = o.EvaluationId == subject.PreviousEvaluationId
            ? subject.PreviousOutcomes
            : subject.PreviousEvaluations.FirstOrDefault(e => e.EvaluationId == o.EvaluationId)?.Outcomes ?? [];
        var previous = evidence.SingleOrDefault(p => p.OutcomeRef == o.OutcomeRef);
        return previous is not null
            && previous.Severity == Severity.SoftStop
            && previous.RuleId == current.RuleId && previous.RuleVersion == current.RuleVersion
            && previous.DistributionLine == current.DistributionLine
            && SameEvidence(previous.Inputs, current.Inputs);
    }

    /// <summary>
    /// An override is granted for specific figures: if the inputs changed, it is a new exception, not the old one.
    /// </summary>
    private static bool SameEvidence(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
