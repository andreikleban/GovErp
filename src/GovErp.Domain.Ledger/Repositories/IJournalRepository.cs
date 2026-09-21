using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IJournalRepository
{
    Task AddAsync(JournalEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> ListBySourceAsync(string sourceRef, CancellationToken ct = default);
}
