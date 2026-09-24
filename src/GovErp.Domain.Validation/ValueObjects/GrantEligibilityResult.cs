namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Whether the grant accepts this expense.
/// </summary>
public enum GrantEligibilityResult { Eligible, GrantNotActive, OutsidePeriod, DepartmentNotAllowed, ObjectNotAllowed }
