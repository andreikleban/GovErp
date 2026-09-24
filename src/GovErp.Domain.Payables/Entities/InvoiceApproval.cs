namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// A recorded approval decision on an invoice cycle.
/// </summary>
public sealed record InvoiceApproval(ApproverRole Role, DepartmentCode? Department, UserId UserId, ApprovalDecision Decision,
    Guid EvaluationRef, int ContentVersion, Guid ApprovalCycleId, string RuleFingerprint, string? Reason, DateTimeOffset At);
