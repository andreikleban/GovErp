using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

public sealed class EfFiscalPeriodRepository(GovErpDbContext db) : IFiscalPeriodRepository
{
    public Task<FiscalPeriod?> FindAsync(int year, int month, CancellationToken ct = default) =>
        db.FiscalPeriods.SingleOrDefaultAsync(p => p.Year == year && p.Month == month, ct);

    public async Task<IReadOnlyList<FiscalPeriod>> ListAsync(CancellationToken ct = default) =>
        await db.FiscalPeriods.OrderBy(p => p.Year).ThenBy(p => p.Month).ToListAsync(ct);
}
