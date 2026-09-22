using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

public sealed class EfGrantRepository(GovErpDbContext db) : IGrantRepository
{
    public Task<Grant?> FindAsync(GrantCode code, CancellationToken ct = default) =>
        db.Grants.SingleOrDefaultAsync(g => g.Code == code, ct);

    public async Task<IReadOnlyList<Grant>> ListAsync(CancellationToken ct = default) =>
        await db.Grants.OrderBy(g => g.Code).ToListAsync(ct);
}
