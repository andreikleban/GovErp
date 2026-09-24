using GovErp.Domain.Validation.DomainServices.Rules;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Rules known to the code. A definition for steps 1–6 without an implementation here is a configuration error.
/// </summary>
public sealed class RuleCatalog
{
    public const string ApprovalRouteRuleId = "APPROVAL_ROUTE";

    private readonly IReadOnlyDictionary<string, IValidationRule> _byId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ParameterSpec>> _parameters;

    public RuleCatalog(IEnumerable<IValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _byId = rules.ToDictionary(rule => rule.RuleId, StringComparer.Ordinal);
        _parameters = _byId.Values
            .Select(rule => (rule.RuleId, rule.Parameters))
            .Append((ApprovalRouteRuleId, ApprovalRouteResolver.Parameters))
            .ToDictionary(entry => entry.Item1, entry => entry.Item2, StringComparer.Ordinal);
    }

    public static RuleCatalog Default { get; } = new(
    [
        new SegRequiredRule(), new SegGrantForbiddenRule(), new CoaCombinationActiveRule(),
        new FundDeptObjectAllowedRule(), new GrantEligibleRule(), new VendorEligibleRule(),
        new ProcurementThresholdRule(), new InvoiceDuplicateRule(), new BudgetAvailabilityRule(),
        new BudgetLowRemainingRule(), new PoLiquidationRule(),
    ]);

    public IValidationRule? Find(string ruleId) => _byId.GetValueOrDefault(ruleId);

    /// <summary>
    /// The meaning of a rule parameter as the code declares it; null when the code does not know the parameter.
    /// </summary>
    public ParameterSpec? ParameterOf(string ruleId, string name) =>
        _parameters.GetValueOrDefault(ruleId)?.FirstOrDefault(spec => spec.Name == name);

    /// <summary>
    /// All implemented rules and the route parameters are mandatory: their absence from the set is a refusal, not a silent skip.
    /// </summary>
    public IReadOnlyCollection<string> MandatoryRuleIds => _byId.Keys.Append(ApprovalRouteRuleId).ToArray();
}
