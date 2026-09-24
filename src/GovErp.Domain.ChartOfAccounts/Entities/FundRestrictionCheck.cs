namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>
/// Result of checking that a fund allows the department and object.
/// </summary>
public enum FundRestrictionCheck { Allowed, DepartmentNotAllowed, ObjectNotAllowed }
