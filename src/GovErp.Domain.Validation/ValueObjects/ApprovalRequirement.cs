namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// A role on the approval route and why it is there (a RouteReasons code with its arguments).
/// </summary>
public sealed record ApprovalRequirement(ApproverRole Role, string? Department, Problem Reason, bool IsSatisfied);
