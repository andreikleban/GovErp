using GovErp.Application.Web.Budget.Contracts;

namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>Fund card: attributes, allowed departments/objects, account combinations and budget lines of this fund (for all years that have periods).</summary>
public sealed record FundDetailVm(string Code, string Name, string Type, string Basis, string ControlMode, string GrantPolicy,
    IReadOnlyList<string> AllowedDepartments, IReadOnlyList<string> AllowedObjects, bool IsActive,
    IReadOnlyList<CombinationVm> Combinations, IReadOnlyList<BudgetLineVm> BudgetLines);
