using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Picks the rule definitions in force on a date for a scope (fund, grant). Which layer wins is LayerPrecedence,
/// whether a local layer may change a rule is OverrideGuard, and the hash of the result is RuleSetFingerprint.
/// </summary>
public sealed class RuleResolver
{
    public const string EngineVersion = "engine-1.0.0";

    private readonly OverrideGuard _guard;

    public RuleResolver(RuleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _guard = new OverrideGuard(catalog);
    }

    public static RuleResolver Default { get; } = new(RuleCatalog.Default);

    public EffectiveRuleSet Resolve(IReadOnlyList<RuleDefinition> candidates, DateOnly onDate, string? fund = null, string? grant = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var inForce = candidates
            .Where(rule => rule.IsDatedFor(onDate) && rule.Covers(fund, grant))
            .GroupBy(rule => rule.RuleId, StringComparer.Ordinal)
            .Select(versions => LayerPrecedence.Winner(versions, _guard))
            .OfType<RuleDefinition>();
        return Assemble(inForce);
    }

    /// <summary>
    /// One set per scope the subject's lines charge, and their union; the fingerprint covers every scope.
    /// </summary>
    public EffectiveRuleSet ResolveForSubject(IReadOnlyList<RuleDefinition> candidates, ValidationSubject subject)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(subject);
        var date = subject.Transaction.InvoiceDate;
        var scopes = subject.Distributions
            .Select(line => (Fund: (string?)line.Account.Fund.Value, Grant: line.Account.Grant?.Value))
            .Distinct()
            .ToDictionary(scope => scope, scope => Resolve(candidates, date, scope.Fund, scope.Grant));
        return scopes.Count == 0
            ? Resolve(candidates, date)
            : Assemble(scopes.Values.SelectMany(set => set.Rules).Distinct(), scopes);
    }

    /// <summary>
    /// Stable order, the highest version per layer, and the fingerprint.
    /// </summary>
    private static EffectiveRuleSet Assemble(IEnumerable<RuleDefinition> rules,
        IReadOnlyDictionary<(string? Fund, string? Grant), EffectiveRuleSet>? scopes = null)
    {
        var ordered = rules
            .OrderBy(rule => rule.Step).ThenBy(rule => rule.RuleId, StringComparer.Ordinal)
            .ThenBy(rule => rule.Layer).ThenBy(rule => rule.Version)
            .ThenBy(rule => rule.ScopeFund, StringComparer.Ordinal).ThenBy(rule => rule.ScopeGrant, StringComparer.Ordinal)
            .ToArray();
        int Highest(RuleLayer layer) => ordered.Where(rule => rule.Layer == layer).Select(rule => rule.Version).DefaultIfEmpty().Max();
        var versions = new RuleSetVersions(EngineVersion, Highest(RuleLayer.Core), Highest(RuleLayer.Federal),
            Highest(RuleLayer.State), Highest(RuleLayer.Tenant))
        {
            Fingerprint = RuleSetFingerprint.Of(ordered, scopes),
            AppliedRules = ordered.Select(rule => new AppliedRuleVersion(rule.RuleId, rule.Layer, rule.Version, rule.ScopeFund, rule.ScopeGrant)).ToArray(),
        };
        return new EffectiveRuleSet(ordered, versions, scopes);
    }
}
