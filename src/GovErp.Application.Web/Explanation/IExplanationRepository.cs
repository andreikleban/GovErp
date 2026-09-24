namespace GovErp.Application.Web.Explanation;

/// <summary>
/// Storage of explanation texts.
/// </summary>
public interface IExplanationRepository
{
    void Add(ExplanationRecord record);
    Task<IReadOnlyList<ExplanationRecord>> ListByEvaluationAsync(Guid evaluationId, CancellationToken ct = default);
}
