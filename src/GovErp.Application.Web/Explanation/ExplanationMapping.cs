namespace GovErp.Application.Web.Explanation;

/// <summary>
/// Builds an explanation view from a stored record.
/// </summary>
public static class ExplanationMapping
{
    public static ExplanationVm ToVm(ExplanationRecord r) =>
        new(r.EvaluationId, r.Audience.ToString(), r.Text, r.Provider, r.Model, r.PromptVersion, r.FallbackReason, r.GeneratedAt);
}
