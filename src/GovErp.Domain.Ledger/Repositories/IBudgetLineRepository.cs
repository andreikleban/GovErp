using GovErp.Domain.Ledger.Entities;
namespace GovErp.Domain.Ledger.Repositories;

public interface IBudgetLineRepository
{
    Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fiscalYear, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default);
    Task AddAsync(BudgetLine line, CancellationToken ct = default);
}
