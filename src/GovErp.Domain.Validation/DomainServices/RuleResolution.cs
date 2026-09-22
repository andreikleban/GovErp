using System.Security.Cryptography;
using System.Text;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public static class RuleResolution
{
    public const string EngineVersion = "engine-1.0.0";

    public static EffectiveRuleSet ResolveForSubject(IReadOnlyList<RuleDefinition> candidates, ValidationSubject subject)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(subject);
        var scopes = subject.Distributions.Select(line => (Fund: (string?)line.Account.Fund.Value, Grant: line.Account.Grant?.Value))
            .Distinct().ToDictionary(scope => scope, scope => Resolve(candidates, subject.Transaction.InvoiceDate, scope.Fund, scope.Grant));
        if (scopes.Count == 0) return Resolve(candidates, subject.Transaction.InvoiceDate);
        return CreateSet(scopes.Values.SelectMany(set => set.Rules).Distinct(), scopes);
    }

    public static EffectiveRuleSet Resolve(IReadOnlyList<RuleDefinition> candidates, DateOnly onDate,
        string? fund = null, string? grant = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var applicable = candidates.Where(rule => onDate >= rule.EffectiveFrom
            && (rule.EffectiveTo is null || onDate <= rule.EffectiveTo)
            && (rule.ScopeFund is null || rule.ScopeFund == fund)
            && (rule.ScopeGrant is null || rule.ScopeGrant == grant)).ToArray();
        var effective = new List<RuleDefinition>();
        foreach (var group in applicable.GroupBy(rule => rule.RuleId, StringComparer.Ordinal))
        {
            if (group.GroupBy(rule => (rule.Layer, rule.Version)).Any(versions => versions.Count() > 1))
                throw new ValidationException($"Rule {group.Key}: ambiguous definitions for the same layer and version.");
            var layers = group.GroupBy(rule => rule.Layer)
                .Select(layer => layer.MaxBy(rule => rule.Version)!).OrderBy(rule => rule.Layer).ToArray();
            for (var i = 0; i < layers.Length; i++)
            {
                if (!layers[i].IsEnabled) continue;
                for (var j = i + 1; j < layers.Length; j++) EnsureSafeOverride(layers[i], layers[j]);
            }
            if (layers[^1].IsEnabled) effective.Add(layers[^1]);
        }
        return CreateSet(effective);
    }

    private static EffectiveRuleSet CreateSet(IEnumerable<RuleDefinition> effective,
        IReadOnlyDictionary<(string? Fund, string? Grant), EffectiveRuleSet>? scopes = null)
    {
        var ordered = effective.OrderBy(rule => rule.Step).ThenBy(rule => rule.RuleId, StringComparer.Ordinal)
            .ThenBy(rule => rule.Layer).ThenBy(rule => rule.Version)
            .ThenBy(rule => rule.ScopeFund, StringComparer.Ordinal).ThenBy(rule => rule.ScopeGrant, StringComparer.Ordinal).ToArray();
        int Max(RuleLayer layer) => ordered.Where(rule => rule.Layer == layer).Select(rule => rule.Version).DefaultIfEmpty().Max();
        var versions = new RuleSetVersions(EngineVersion, Max(RuleLayer.Core), Max(RuleLayer.Federal), Max(RuleLayer.State), Max(RuleLayer.Tenant))
        {
            Fingerprint = Fingerprint(ordered, scopes),
            AppliedRules = ordered.Select(rule => new AppliedRuleVersion(rule.RuleId, rule.Layer, rule.Version, rule.ScopeFund, rule.ScopeGrant)).ToArray()
        };
        return new EffectiveRuleSet(ordered, versions, scopes);
    }

    private static void EnsureSafeOverride(RuleDefinition baseline, RuleDefinition local)
    {
        void Reject() => throw new ValidationException($"Rule {baseline.RuleId}: {local.Layer} cannot weaken or replace the {baseline.Layer} guard.");
        if (!local.IsEnabled || local.Step != baseline.Step
            || (baseline.Severity is null ? local.Severity is not null : local.Severity is null || local.Severity < baseline.Severity)
            || local.OverridableBy.Except(baseline.OverridableBy).Any()) Reject();
        if (local.Parameters.Count != baseline.Parameters.Count) Reject();
        foreach (var (key, value) in baseline.Parameters)
        {
            if (!local.Parameters.TryGetValue(key, out var replacement)) { Reject(); continue; }
            if (value == replacement) continue;
            if (baseline.OverridableBy.Count == 0) Reject();
            // Raising a trigger threshold or tolerance removes protections; low-remaining pct is the inverse.
            var safe = key switch
            {
                "threshold" or "finance_director_threshold" or "tolerance_pct" => local.DecimalParameter(key) >= 0
                    && local.DecimalParameter(key) <= baseline.DecimalParameter(key),
                "pct" => local.DecimalParameter(key) >= baseline.DecimalParameter(key) && local.DecimalParameter(key) <= 1,
                _ => false
            };
            if (!safe) Reject();
        }
    }

    private static string Fingerprint(IReadOnlyList<RuleDefinition> rules,
        IReadOnlyDictionary<(string? Fund, string? Grant), EffectiveRuleSet>? scopes = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            // Length-prefixed strings and explicit null markers avoid delimiter collisions.
            writer.Write(EngineVersion);
            writer.Write(rules.Count);
            foreach (var rule in rules)
            {
                writer.Write(rule.RuleId);
                writer.Write(rule.Version);
                writer.Write((int)rule.Layer);
                writer.Write((int)rule.Step);
                WriteNullable(writer, rule.ScopeFund);
                WriteNullable(writer, rule.ScopeGrant);
                writer.Write(rule.Severity.HasValue);
                if (rule.Severity.HasValue) writer.Write((int)rule.Severity.Value);
                writer.Write(rule.IsEnabled);
                writer.Write(rule.EffectiveFrom.DayNumber);
                writer.Write(rule.EffectiveTo.HasValue);
                if (rule.EffectiveTo.HasValue) writer.Write(rule.EffectiveTo.Value.DayNumber);
                writer.Write(rule.Message);
                writer.Write(rule.Resolution);
                writer.Write(rule.Parameters.Count);
                foreach (var pair in rule.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
                writer.Write(rule.OverridableBy.Count);
                foreach (var role in rule.OverridableBy.Order()) writer.Write((int)role);
            }
            if (scopes is not null)
            {
                writer.Write(scopes.Count);
                foreach (var scope in scopes.OrderBy(pair => pair.Key.Fund, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Key.Grant, StringComparer.Ordinal))
                {
                    WriteNullable(writer, scope.Key.Fund);
                    WriteNullable(writer, scope.Key.Grant);
                    writer.Write(scope.Value.Fingerprint);
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteNullable(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null) writer.Write(value);
    }
}



