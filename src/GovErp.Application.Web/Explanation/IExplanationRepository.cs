namespace GovErp.Application.Web.Explanation;

public interface IExplanationRepository
{
    void Add(ExplanationRecord record);
    Task<IReadOnlyList<ExplanationRecord>> ListByEvaluationAsync(Guid evaluationId, CancellationToken ct = default);
}
