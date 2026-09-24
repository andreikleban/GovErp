using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// The rule set in force can be evaluated: every configured rule has code, and every mandatory rule is in force for
/// every scope the invoice charges. Checked before the steps, so an unknown rule cannot hide behind an early Hard Stop.
/// </summary>
internal sealed class ConfigurationCheck
{
    private readonly RuleCatalog _catalog;

    public ConfigurationCheck(RuleCatalog catalog) => _catalog = catalog;

    /// <summary>The first problem, or null; refused as RULE_CONFIGURATION.</summary>
    public string? Problem(ValidationSubject subject, EffectiveRuleSet rules) =>
        RulesWithoutCode(rules) ?? MissingMandatoryRules(subject, rules);

    private string? RulesWithoutCode(EffectiveRuleSet rules)
    {
        var unknown = rules.Rules
            .Where(rule => rule.Step <= ValidationStep.EncumbranceImpact && _catalog.Find(rule.RuleId) is null)
            .Select(rule => rule.RuleId).Distinct().ToArray();
        return unknown.Length > 0 ? $"Configured rules have no implementation: {string.Join(", ", unknown)}." : null;
    }

    private string? MissingMandatoryRules(ValidationSubject subject, EffectiveRuleSet rules)
    {
        foreach (var (fund, grant) in subject.Distributions.Select(d => ((string?)d.Account.Fund.Value, d.Account.Grant?.Value)).Distinct())
        {
            var inForce = rules.ForScope(fund, grant).Rules.Select(rule => rule.RuleId).ToHashSet(StringComparer.Ordinal);
            var missing = _catalog.MandatoryRuleIds.Where(id => !inForce.Contains(id)).ToArray();
            if (missing.Length > 0)
            {
                return $"Mandatory rules are missing or disabled for fund {fund}: {string.Join(", ", missing)}.";
            }
        }

        return null;
    }
}
