using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IJournalRepository
{
    Task AddAsync(JournalEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> ListBySourceAsync(string sourceRef, CancellationToken ct = default);
    /// <summary>Все проводки тенанта: экран General Ledger › Journal, фильтруется в сервисе (фонд, период, источник).</summary>
    Task<IReadOnlyList<JournalEntry>> ListAsync(CancellationToken ct = default);
}
