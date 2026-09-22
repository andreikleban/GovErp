namespace GovErp.Application.Web.Explanation;

public sealed record ExplanationResult(string Text, string Provider, string? Model, string PromptVersion, string? FallbackReason);
