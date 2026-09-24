using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

/// <summary>
/// Collection of journal entries.
/// </summary>
public interface IJournalRepository
{
    Task AddAsync(JournalEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> ListBySourceAsync(string sourceRef, CancellationToken ct = default);
    /// <summary>
    /// All tenant journal entries: the General Ledger › Journal screen, filtered in the service (fund, period, source).
    /// </summary>
    Task<IReadOnlyList<JournalEntry>> ListAsync(CancellationToken ct = default);
}
