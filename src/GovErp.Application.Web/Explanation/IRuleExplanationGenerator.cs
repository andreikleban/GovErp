using GovErp.Application.Web.Reference.Contracts;

namespace GovErp.Application.Web.Explanation;

/// <summary>
/// Пересказ правила для аудитории. Источник фактов — написанное описание и действующая версия правила;
/// при недоступной модели возвращается само описание с причиной отката.
/// </summary>
public interface IRuleExplanationGenerator
{
    Task<ExplanationResult> ExplainAsync(RuleDescriptionVm description, RuleVm? current, ExplanationAudience audience, CancellationToken ct = default);
}
