using GovErp.Domain.Validation.Entities;

namespace GovErp.Application.Web.Explanation;

public interface IExplanationGenerator
{
    Task<ExplanationResult> ExplainAsync(EvaluationRecord record, ExplanationAudience audience, CancellationToken ct = default);
}
