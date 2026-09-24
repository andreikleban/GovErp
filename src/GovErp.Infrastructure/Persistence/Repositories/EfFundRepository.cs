using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL repository of funds for the current tenant.
/// </summary>
public sealed class EfFundRepository(GovErpDbContext db) : IFundRepository
{
    public Task<Fund?> FindAsync(FundCode code, CancellationToken ct = default) =>
        db.Funds.SingleOrDefaultAsync(f => f.Code == code, ct);

    public async Task<IReadOnlyList<Fund>> ListAsync(CancellationToken ct = default) =>
        await db.Funds.OrderBy(f => f.Code).ToListAsync(ct);
}
