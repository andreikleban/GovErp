namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// Написанное описание правила: что проверяет, на каких фактах, когда срабатывает и как снимается,
/// откуда правило взялось и что требует подтверждения экспертом. Источник правды для окна Description и для LLM.
/// </summary>
public sealed record RuleDescriptionVm(string RuleId, int Step, string StepName, string Title, string Checks,
    IReadOnlyList<string> Facts, IReadOnlyDictionary<string, string> Parameters, string WhenFired, string Resolution,
    string Source, IReadOnlyList<string> SmeQuestions);
