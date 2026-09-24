using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IFiscalPeriodRepository
{
    Task<FiscalPeriod?> FindAsync(int year, int month, CancellationToken ct = default);
    /// <summary>All tenant periods: the General Ledger › Periods screen.</summary>
    Task<IReadOnlyList<FiscalPeriod>> ListAsync(CancellationToken ct = default);
}
