using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IFiscalPeriodRepository
{
    Task<FiscalPeriod?> FindAsync(int year, int month, CancellationToken ct = default);
    /// <summary>Все периоды тенанта: экран General Ledger › Periods.</summary>
    Task<IReadOnlyList<FiscalPeriod>> ListAsync(CancellationToken ct = default);
}
