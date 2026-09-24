namespace GovErp.Domain.Validation.Codes;

/// <summary>
/// Codes of the reasons a role is on the approval route (step 7).
/// </summary>
public static class RouteReasons
{
    public const string DepartmentCharged = "ROUTE.DEPARTMENT_CHARGED";
    public const string GrantFunded = "ROUTE.GRANT_FUNDED";
    public const string FinanceDirectorThreshold = "ROUTE.FINANCE_DIRECTOR_THRESHOLD";
    public const string OverrideRequired = "ROUTE.OVERRIDE_REQUIRED";
}
