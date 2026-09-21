namespace GovErp.Domain.Payables.Entities;

public sealed record ApprovalRequirement(ApproverRole Role, DepartmentCode? Department);
