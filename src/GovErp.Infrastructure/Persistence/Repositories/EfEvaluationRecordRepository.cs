using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

public sealed class EfEvaluationRecordRepository(GovErpDbContext db) : IEvaluationRecordRepository
{
    public async Task AddAsync(EvaluationRecord record, CancellationToken ct = default) =>
        await db.Set<EvaluationRecord>().AddAsync(record, ct);

    public Task<EvaluationRecord?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.EvaluationRecords.SingleOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<EvaluationRecord>> ListByTransactionAsync(string transactionRef, CancellationToken ct = default) =>
        await db.EvaluationRecords.Where(e => e.TransactionRef == transactionRef).OrderBy(e => e.EvaluatedAt).ToListAsync(ct);
}
