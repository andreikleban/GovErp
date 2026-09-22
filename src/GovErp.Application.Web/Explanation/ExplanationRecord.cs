namespace GovErp.Application.Web.Explanation;

public sealed record ExplanationRecord(Guid Id, Guid EvaluationId, string TransactionRef, ExplanationAudience Audience, string Text,
    string Provider, string? Model, string PromptVersion, string? FallbackReason, DateTimeOffset GeneratedAt);
