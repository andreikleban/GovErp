namespace GovErp.Domain.Validation.ValueObjects;

public sealed record OverrideSnapshot(string RuleId, UserId UserId, string Reason,
    int RuleVersion = 0, int? DistributionLine = null, int ContentVersion = 0,
    Guid ApprovalCycleId = default, string RuleFingerprint = "", Guid EvaluationId = default,
    ApproverRole? Role = null, Guid OutcomeRef = default);
