namespace GovErp.Application.Web.Explanation;

public static class ExplanationMapping
{
    public static ExplanationVm ToVm(ExplanationRecord r) =>
        new(r.EvaluationId, r.Audience.ToString(), r.Text, r.Provider, r.Model, r.PromptVersion, r.FallbackReason, r.GeneratedAt);
}
