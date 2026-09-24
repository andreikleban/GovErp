using System.Security.Cryptography;
using System.Text;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// SHA-256 over every field of the rules in force (and of each scope's set). An approval remembers the fingerprint;
/// a different fingerprint at Post means the rules changed and the invoice goes back to approval (REVALIDATION_REQUIRED).
/// The byte layout must not change: stored fingerprints of approved invoices are compared with new ones.
/// </summary>
internal static class RuleSetFingerprint
{
    public static string Of(IReadOnlyList<RuleDefinition> rules,
        IReadOnlyDictionary<(string? Fund, string? Grant), EffectiveRuleSet>? scopes = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            // Length-prefixed strings and explicit null markers avoid delimiter collisions.
            writer.Write(RuleResolver.EngineVersion);
            writer.Write(rules.Count);
            foreach (var rule in rules)
            {
                Write(writer, rule);
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

    private static void Write(BinaryWriter writer, RuleDefinition rule)
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

    private static void WriteNullable(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null) writer.Write(value);
    }
}
