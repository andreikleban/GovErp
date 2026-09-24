namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>
/// Result of checking an expense against a grant.
/// </summary>
public enum GrantEligibility { Eligible, GrantNotActive, OutsidePeriod, DepartmentNotAllowed, ObjectNotAllowed }
