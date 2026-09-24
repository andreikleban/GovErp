using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Picks the definition in force among the dated versions of one rule: the latest version in each layer, then the most
/// local layer (Core → Federal → State → Tenant), provided it keeps every guard of the layers above it.
/// </summary>
internal static class LayerPrecedence
{
    /// <summary>
    /// The winning definition; null when the winner is disabled (the rule is switched off).
    /// </summary>
    public static RuleDefinition? Winner(IEnumerable<RuleDefinition> versions, OverrideGuard guard)
    {
        var layers = LatestPerLayer(versions.ToList());
        foreach (var upper in layers.Where(definition => definition.IsEnabled))
        {
            foreach (var local in layers.Where(definition => definition.Layer > upper.Layer))
            {
                guard.EnsureKept(upper, local);
            }
        }

        var winner = layers[^1];
        return winner.IsEnabled ? winner : null;
    }

    /// <summary>
    /// One definition per layer, ordered from Core to Tenant. Two definitions with the same layer and version are ambiguous.
    /// </summary>
    private static IReadOnlyList<RuleDefinition> LatestPerLayer(IReadOnlyList<RuleDefinition> versions)
    {
        if (versions.GroupBy(definition => (definition.Layer, definition.Version)).Any(same => same.Count() > 1))
        {
            throw new ValidationException(ValidationErrors.AmbiguousDefinitions, ("rule", versions[0].RuleId));
        }

        return versions
            .GroupBy(definition => definition.Layer)
            .Select(layer => layer.MaxBy(definition => definition.Version)!)
            .OrderBy(definition => definition.Layer)
            .ToList();
    }
}
