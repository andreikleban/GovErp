namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceApproval(ApproverRole Role, DepartmentCode? Department, UserId UserId, ApprovalDecision Decision,
    Guid EvaluationRef, int ContentVersion, Guid ApprovalCycleId, string RuleFingerprint, string? Reason, DateTimeOffset At);
