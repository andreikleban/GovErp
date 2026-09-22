using GovErp.Domain.Validation.DomainServices.Rules;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Правила, известные коду. Определение шагов 1–6 без реализации здесь — ошибка конфигурации.</summary>
public sealed class RuleCatalog
{
    public const string ApprovalRouteRuleId = "APPROVAL_ROUTE";

    private readonly IReadOnlyDictionary<string, IValidationRule> _byId;

    public RuleCatalog(IEnumerable<IValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _byId = rules.ToDictionary(rule => rule.RuleId, StringComparer.Ordinal);
    }

    public static RuleCatalog Default { get; } = new(
    [
        new SegRequiredRule(), new SegGrantForbiddenRule(), new CoaCombinationActiveRule(),
        new FundDeptObjectAllowedRule(), new GrantEligibleRule(), new VendorEligibleRule(),
        new ProcurementThresholdRule(), new InvoiceDuplicateRule(), new BudgetAvailabilityRule(),
        new BudgetLowRemainingRule(), new PoLiquidationRule(),
    ]);

    public IValidationRule? Find(string ruleId) => _byId.GetValueOrDefault(ruleId);

    /// <summary>Обязательны все реализованные правила и параметры маршрута: их отсутствие в наборе — отказ, а не молчаливый пропуск.</summary>
    public IReadOnlyCollection<string> MandatoryRuleIds => _byId.Keys.Append(ApprovalRouteRuleId).ToArray();
}
