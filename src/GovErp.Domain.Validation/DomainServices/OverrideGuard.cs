using System.Diagnostics.CodeAnalysis;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// A more local layer (Tenant below State below Federal below Core) may tighten a rule of a higher layer but never weaken
/// or replace it. Whether a parameter change tightens the rule is declared by the rule itself (ParameterSpec).
/// </summary>
internal sealed class OverrideGuard
{
    private readonly RuleCatalog _catalog;

    public OverrideGuard(RuleCatalog catalog) => _catalog = catalog;

    public void EnsureKept(RuleDefinition upper, RuleDefinition local)
    {
        if (!local.IsEnabled) Reject(upper, local, "switches it off");
        if (local.Step != upper.Step) Reject(upper, local, "moves it to another step");
        if (local.IsMilderThan(upper)) Reject(upper, local, "lowers its severity");
        if (local.AllowsMoreOverridersThan(upper)) Reject(upper, local, "lets more roles override it");
        if (!local.Parameters.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(upper.Parameters.Keys))
            Reject(upper, local, "changes its parameter set");

        foreach (var (name, value) in upper.Parameters)
        {
            if (local.Parameters[name] == value) continue;
            if (upper.OverridableBy.Count == 0) Reject(upper, local, $"changes {name} of a guard nobody may override");
            var spec = _catalog.ParameterOf(upper.RuleId, name);
            if (spec is null) Reject(upper, local, $"changes {name}, whose direction the code does not declare");
            if (!spec.KeepsGuard(upper.DecimalParameter(name), local.DecimalParameter(name))) Reject(upper, local, $"relaxes {name}");
        }
    }

    [DoesNotReturn]
    private static void Reject(RuleDefinition upper, RuleDefinition local, string how) =>
        throw new ValidationException(
            $"Rule {upper.RuleId}: the {local.Layer} layer {how}; it cannot weaken or replace the {upper.Layer} guard.");
}
