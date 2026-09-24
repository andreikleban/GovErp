using GovErp.Domain.Ledger.Entities;

namespace GovErp.Domain.Ledger.Repositories;

/// <summary>
/// Collection of opening balances.
/// </summary>
public interface IOpeningBalanceRepository
{
    Task<IReadOnlyList<OpeningBalance>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default);
    Task AddAsync(OpeningBalance balance, CancellationToken ct = default);
}
