using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL repository of journal entries for the current tenant.
/// </summary>
public sealed class EfJournalRepository(GovErpDbContext db) : IJournalRepository
{
    public async Task AddAsync(JournalEntry entry, CancellationToken ct = default) =>
        await db.Set<JournalEntry>().AddAsync(entry, ct);

    public async Task<IReadOnlyList<JournalEntry>> ListBySourceAsync(string sourceRef, CancellationToken ct = default) =>
        await db.JournalEntries.Where(j => j.SourceRef == sourceRef).OrderBy(j => j.PostedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<JournalEntry>> ListAsync(CancellationToken ct = default) =>
        await db.JournalEntries.OrderByDescending(j => j.PostedAt).ToListAsync(ct);
}
