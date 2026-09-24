namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// A required approval step: a role, and a department when the step is departmental.
/// </summary>
public sealed record ApprovalRequirement(ApproverRole Role, DepartmentCode? Department);
