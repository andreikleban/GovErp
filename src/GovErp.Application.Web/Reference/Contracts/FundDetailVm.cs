using GovErp.Application.Web.Budget.Contracts;

namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>Карточка фонда: атрибуты, разрешённые отделы/объекты, комбинации счетов и строки бюджета этого фонда (по всем годам, у которых есть периоды).</summary>
public sealed record FundDetailVm(string Code, string Name, string Type, string Basis, string ControlMode, string GrantPolicy,
    IReadOnlyList<string> AllowedDepartments, IReadOnlyList<string> AllowedObjects, bool IsActive,
    IReadOnlyList<CombinationVm> Combinations, IReadOnlyList<BudgetLineVm> BudgetLines);
