using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IBudgetLineRepository
{
    Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fiscalYear, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default);
    /// <summary>Строка, которой принадлежит резерв (ссылки на резервы хранит инвойс).</summary>
    Task<BudgetLine?> FindByReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task AddAsync(BudgetLine line, CancellationToken ct = default);
}
