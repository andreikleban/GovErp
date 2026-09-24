namespace GovErp.Application.Web.Common;

/// <summary>
/// Application role names and the groups built from them (approvers, posters).
/// </summary>
public static class Roles
{
    public const string ApClerk = "ApClerk";
    public const string DepartmentHead = "DepartmentHead";
    public const string GrantsManager = "GrantsManager";
    public const string BudgetOfficer = "BudgetOfficer";
    public const string FinanceDirector = "FinanceDirector";
    public static readonly string[] Approvers = [DepartmentHead, GrantsManager, BudgetOfficer, FinanceDirector];
    public static readonly string[] Overriders = [BudgetOfficer, FinanceDirector];
    public static readonly string[] Posters = [BudgetOfficer, FinanceDirector];
}
