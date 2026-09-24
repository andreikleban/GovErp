using GovErp.Application.Web.Reference.Contracts;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;

namespace GovErp.Application.Web.Reference;

internal static class RuleVmMapping
{
    /// <summary>Версии, которые применяются на дату: общий набор плюс scoped-версии в своём фонде и гранте.</summary>
    public static HashSet<Guid> CurrentIds(IReadOnlyList<RuleDefinition> all, DateOnly onDate)
    {
        var current = RuleResolution.Resolve(all, onDate).Rules.Select(r => r.Id).ToHashSet();
        foreach (var scope in all.Where(r => r.ScopeFund is not null || r.ScopeGrant is not null).Select(r => (r.ScopeFund, r.ScopeGrant)).Distinct())
        {
            current.UnionWith(RuleResolution.Resolve(all, onDate, scope.ScopeFund, scope.ScopeGrant).Rules
                .Where(r => r.ScopeFund == scope.ScopeFund && r.ScopeGrant == scope.ScopeGrant).Select(r => r.Id));
        }

        return current;
    }

    public static RuleVm ToVm(RuleDefinition r, bool isCurrent) =>
        new(r.Id, r.RuleId, r.Version, (int)r.Step, r.Layer.ToString(), r.ScopeFund, r.ScopeGrant, r.Severity?.ToString(),
            r.Parameters, r.OverridableBy.Select(role => role.ToString()).ToList(), r.EffectiveFrom, r.EffectiveTo, r.IsEnabled, r.Message,
            isCurrent);
}
