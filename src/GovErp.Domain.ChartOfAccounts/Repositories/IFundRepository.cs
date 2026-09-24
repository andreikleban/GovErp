using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Repositories;

/// <summary>
/// Collection of funds.
/// </summary>
public interface IFundRepository
{
    Task<Fund?> FindAsync(FundCode code, CancellationToken ct = default);
    Task<IReadOnlyList<Fund>> ListAsync(CancellationToken ct = default);
}
