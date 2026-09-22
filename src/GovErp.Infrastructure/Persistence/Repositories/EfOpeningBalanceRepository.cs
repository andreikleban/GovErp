using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

public sealed class EfOpeningBalanceRepository(GovErpDbContext db) : IOpeningBalanceRepository
{
    public async Task<IReadOnlyList<OpeningBalance>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default) =>
        await db.OpeningBalances.Where(o => o.FiscalYear == fiscalYear).OrderBy(o => o.AsOfDate).ToListAsync(ct);

    public async Task AddAsync(OpeningBalance balance, CancellationToken ct = default) =>
        await db.Set<OpeningBalance>().AddAsync(balance, ct);
}
