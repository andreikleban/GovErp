using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Repositories;

public interface IAccountCombinationRepository
{
    Task<AccountCombination?> FindAsync(AccountCode code, CancellationToken ct = default);
    Task<IReadOnlyList<AccountCombination>> ListAsync(CancellationToken ct = default);
    Task AddAsync(AccountCombination combination, CancellationToken ct = default);
}
