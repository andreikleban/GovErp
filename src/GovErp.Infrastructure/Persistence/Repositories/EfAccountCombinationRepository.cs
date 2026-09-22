using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

public sealed class EfAccountCombinationRepository(GovErpDbContext db) : IAccountCombinationRepository
{
    public Task<AccountCombination?> FindAsync(AccountCode code, CancellationToken ct = default) =>
        db.AccountCombinations.SingleOrDefaultAsync(c => c.Code == code, ct);

    public async Task<IReadOnlyList<AccountCombination>> ListAsync(CancellationToken ct = default) =>
        await db.AccountCombinations.OrderBy(c => c.Code).ToListAsync(ct);

    public async Task AddAsync(AccountCombination combination, CancellationToken ct = default) =>
        await db.Set<AccountCombination>().AddAsync(combination, ct);
}
