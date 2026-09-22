namespace GovErp.Domain.Validation.ValueObjects;

public sealed record ApprovalRequirement(ApproverRole Role, string? Department, string Reason, bool IsSatisfied);
