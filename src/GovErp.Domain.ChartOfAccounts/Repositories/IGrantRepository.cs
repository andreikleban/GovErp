using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Repositories;

/// <summary>
/// Collection of grants.
/// </summary>
public interface IGrantRepository
{
    Task<Grant?> FindAsync(GrantCode code, CancellationToken ct = default);
    Task<IReadOnlyList<Grant>> ListAsync(CancellationToken ct = default);
}
