using GovErp.Domain.Validation.Entities;

namespace GovErp.Application.Web.Explanation;

/// <summary>
/// Builds explanation text from an evaluation record.
/// </summary>
public interface IExplanationGenerator
{
    Task<ExplanationResult> ExplainAsync(EvaluationRecord record, ExplanationAudience audience, CancellationToken ct = default);
}
