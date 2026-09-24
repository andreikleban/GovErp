using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.Repositories;

/// <summary>
/// Collection of stored evaluations.
/// </summary>
public interface IEvaluationRecordRepository
{
    Task AddAsync(EvaluationRecord record, CancellationToken ct = default);
    Task<EvaluationRecord?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationRecord>> ListByTransactionAsync(string transactionRef, CancellationToken ct = default);
    /// <summary>
    /// All tenant evaluations, unsorted and unlimited; Audit itself takes the latest 200 after filters.
    /// </summary>
    Task<IReadOnlyList<EvaluationRecord>> ListAsync(CancellationToken ct = default);
}
