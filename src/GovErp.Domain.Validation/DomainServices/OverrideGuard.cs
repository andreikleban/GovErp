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
        if (!local.IsEnabled) Reject(ValidationErrors.LayerSwitchesOff, upper, local);
        if (local.Step != upper.Step) Reject(ValidationErrors.LayerMovesStep, upper, local);
        if (local.IsMilderThan(upper)) Reject(ValidationErrors.LayerLowersSeverity, upper, local);
        if (local.AllowsMoreOverridersThan(upper)) Reject(ValidationErrors.LayerAddsOverriders, upper, local);
        if (!local.Parameters.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(upper.Parameters.Keys))
            Reject(ValidationErrors.LayerChangesParameterSet, upper, local);

        foreach (var (name, value) in upper.Parameters)
        {
            if (local.Parameters[name] == value) continue;
            if (upper.OverridableBy.Count == 0) Reject(ValidationErrors.LayerChangesFixedParameter, upper, local, name);
            var spec = _catalog.ParameterOf(upper.RuleId, name);
            if (spec is null) Reject(ValidationErrors.LayerChangesUndeclaredParameter, upper, local, name);
            if (!spec.KeepsGuard(upper.DecimalParameter(name), local.DecimalParameter(name)))
                Reject(ValidationErrors.LayerRelaxesParameter, upper, local, name);
        }
    }

    [DoesNotReturn]
    private static void Reject(string code, RuleDefinition upper, RuleDefinition local, string? parameter = null) =>
        throw new ValidationException(code, ("rule", upper.RuleId), ("layer", local.Layer), ("upperLayer", upper.Layer), ("parameter", parameter));
}
