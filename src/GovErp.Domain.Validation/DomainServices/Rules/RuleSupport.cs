using System.Globalization;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

internal static class RuleSupport
{
    public static Dictionary<string, string> Inputs(DistributionSnapshot d, params (string Key, string Value)[] extra)
    {
        var map = new Dictionary<string, string>
        {
            ["line"] = d.LineNo.ToString(CultureInfo.InvariantCulture),
            ["account"] = d.Account.ToString(),
            ["amount"] = d.Amount.ToString(),
        };
        foreach (var (k, v) in extra)
        {
            map[k] = v;
        }

        return map;
    }

    public static Dictionary<string, string> Map(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    public static RuleOutcome MissingFact(RuleDefinition definition, DistributionSnapshot distribution, string fact) =>
        RuleOutcome.From(definition, Severity.HardStop, distribution.LineNo,
            Inputs(distribution), Map(("missingFact", fact)),
            $"Required {fact} facts are missing for line {distribution.LineNo}.");
}

