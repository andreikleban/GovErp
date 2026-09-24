namespace GovErp.Application.Web.Explanation;

/// <summary>
/// An evaluation explanation as shown on screen.
/// </summary>
public sealed record ExplanationVm(Guid EvaluationId, string Audience, string Text, string Provider, string? Model, string PromptVersion,
    string? FallbackReason, DateTimeOffset GeneratedAt);
