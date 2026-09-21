using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Repositories;

public interface IGrantRepository
{
    Task<Grant?> FindAsync(GrantCode code, CancellationToken ct = default);
    Task<IReadOnlyList<Grant>> ListAsync(CancellationToken ct = default);
}
