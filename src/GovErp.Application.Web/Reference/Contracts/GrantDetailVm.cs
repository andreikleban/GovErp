using GovErp.Application.Web.Budget.Contracts;

namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>Grant card: sponsor, period, allowed departments/objects, status, account combinations and budget lines of this grant.</summary>
public sealed record GrantDetailVm(string Code, string Name, string Sponsor, bool IsFederal, DateOnly PeriodFrom, DateOnly? PeriodTo,
    IReadOnlyList<string> AllowedDepartments, IReadOnlyList<string> AllowedObjects, string Status,
    IReadOnlyList<CombinationVm> Combinations, IReadOnlyList<BudgetLineVm> BudgetLines);
