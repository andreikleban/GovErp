using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.Repositories;

public interface IEvaluationRecordRepository
{
    Task AddAsync(EvaluationRecord record, CancellationToken ct = default);
    Task<EvaluationRecord?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationRecord>> ListByTransactionAsync(string transactionRef, CancellationToken ct = default);
}
