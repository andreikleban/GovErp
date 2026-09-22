using System.Collections.ObjectModel;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;

namespace GovErp.Domain.Validation.ValueObjects;

public sealed class EffectiveRuleSet
{
    private readonly IReadOnlyDictionary<(string? Fund, string? Grant), EffectiveRuleSet>? _scopes;
    public IReadOnlyList<RuleDefinition> Rules { get; }
    public IReadOnlyList<RuleDefinition> Definitions => Rules;
    public RuleSetVersions Versions { get; }
    public string Fingerprint => Versions.Fingerprint;

    internal EffectiveRuleSet(IEnumerable<RuleDefinition> definitions, RuleSetVersions versions,
        IReadOnlyDictionary<(string? Fund, string? Grant), EffectiveRuleSet>? scopes = null)
    {
        Rules = Array.AsReadOnly(definitions.ToArray());
        Versions = versions;
        _scopes = scopes is null ? null : new ReadOnlyDictionary<(string? Fund, string? Grant), EffectiveRuleSet>(
            new Dictionary<(string? Fund, string? Grant), EffectiveRuleSet>(scopes));
    }

    public IReadOnlyList<RuleDefinition> ForStep(ValidationStep step) =>
        Array.AsReadOnly(Rules.Where(rule => rule.Step == step).ToArray());

    public EffectiveRuleSet ForScope(string? fund, string? grant) => _scopes is null ? this
        : _scopes.TryGetValue((fund, grant), out var set) ? set
        : throw new ValidationException($"No resolved rule set for fund '{fund}' and grant '{grant}'.");

    public RuleDefinition? Find(string ruleId, string? fund, string? grant) => ForScope(fund, grant).Find(ruleId);

    public RuleDefinition? Find(string ruleId)
    {
        var matches = Rules.Where(rule => rule.RuleId == ruleId).Take(2).ToArray();
        return matches.Length > 1
            ? throw new ValidationException($"Rule {ruleId} has multiple effective scopes; specify fund and grant.")
            : matches.SingleOrDefault();
    }
}
