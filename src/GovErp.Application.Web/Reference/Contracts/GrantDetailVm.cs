using GovErp.Application.Web.Budget.Contracts;

namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>Карточка гранта: спонсор, период, разрешённые отделы/объекты, статус, комбинации счетов и строки бюджета этого гранта.</summary>
public sealed record GrantDetailVm(string Code, string Name, string Sponsor, bool IsFederal, DateOnly PeriodFrom, DateOnly? PeriodTo,
    IReadOnlyList<string> AllowedDepartments, IReadOnlyList<string> AllowedObjects, string Status,
    IReadOnlyList<CombinationVm> Combinations, IReadOnlyList<BudgetLineVm> BudgetLines);
