namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// An approver's decision as it stood when the evaluation ran.
/// </summary>
public sealed record ApprovalSnapshot(ApproverRole Role, UserId UserId, DepartmentCode? Department,
    int ContentVersion, Guid ApprovalCycleId, string RuleFingerprint, Guid EvaluationId, bool IsApproved = true);
