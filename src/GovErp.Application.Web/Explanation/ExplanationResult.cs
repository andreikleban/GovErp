namespace GovErp.Application.Web.Explanation;

/// <summary>
/// What a generator returned: text, provider, and a fallback reason when it did not.
/// </summary>
public sealed record ExplanationResult(string Text, string Provider, string? Model, string PromptVersion, string? FallbackReason);
