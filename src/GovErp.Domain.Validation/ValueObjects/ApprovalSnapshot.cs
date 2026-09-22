namespace GovErp.Domain.Validation.ValueObjects;

public sealed record ApprovalSnapshot(ApproverRole Role, UserId UserId, DepartmentCode? Department,
    int ContentVersion, Guid ApprovalCycleId, string RuleFingerprint, Guid EvaluationId, bool IsApproved = true);
