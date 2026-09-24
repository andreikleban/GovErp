namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// A released soft stop, bound to one evaluation outcome.
/// </summary>
public sealed record OverrideSnapshot(string RuleId, UserId UserId, string Reason,
    int RuleVersion = 0, int? DistributionLine = null, int ContentVersion = 0,
    Guid ApprovalCycleId = default, string RuleFingerprint = "", Guid EvaluationId = default,
    ApproverRole? Role = null, Guid OutcomeRef = default);
