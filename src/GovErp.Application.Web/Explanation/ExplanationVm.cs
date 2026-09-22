namespace GovErp.Application.Web.Explanation;

public sealed record ExplanationVm(Guid EvaluationId, string Audience, string Text, string Provider, string? Model, string PromptVersion,
    string? FallbackReason, DateTimeOffset GeneratedAt);
