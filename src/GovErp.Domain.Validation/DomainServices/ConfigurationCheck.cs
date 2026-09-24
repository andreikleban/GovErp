using GovErp.Domain.Validation.Exceptions;
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

    /// <summary>
    /// The first problem, or null; refused as RULE_CONFIGURATION.
    /// </summary>
    public Problem? FirstProblem(ValidationSubject subject, EffectiveRuleSet rules) =>
        RulesWithoutCode(rules) ?? MissingMandatoryRules(subject, rules);

    private Problem? RulesWithoutCode(EffectiveRuleSet rules)
    {
        var unknown = rules.Rules
            .Where(rule => rule.Step <= ValidationStep.EncumbranceImpact && _catalog.Find(rule.RuleId) is null)
            .Select(rule => rule.RuleId).Distinct().ToArray();
        return unknown.Length > 0 ? Problem.Of(ValidationErrors.RulesWithoutCode, ("rules", string.Join(", ", unknown))) : null;
    }

    private Problem? MissingMandatoryRules(ValidationSubject subject, EffectiveRuleSet rules)
    {
        foreach (var (fund, grant) in subject.Distributions.Select(d => ((string?)d.Account.Fund.Value, d.Account.Grant?.Value)).Distinct())
        {
            var inForce = rules.ForScope(fund, grant).Rules.Select(rule => rule.RuleId).ToHashSet(StringComparer.Ordinal);
            var missing = _catalog.MandatoryRuleIds.Where(id => !inForce.Contains(id)).ToArray();
            if (missing.Length > 0)
            {
                return Problem.Of(ValidationErrors.MandatoryRulesMissing, ("fund", fund), ("grant", grant), ("rules", string.Join(", ", missing)));
            }
        }

        return null;
    }
}
