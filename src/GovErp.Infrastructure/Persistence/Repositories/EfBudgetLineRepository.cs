using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL repository of budget lines for the current tenant.
/// </summary>
public sealed class EfBudgetLineRepository(GovErpDbContext db) : IBudgetLineRepository
{
    public Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fiscalYear, CancellationToken ct = default) =>
        db.BudgetLines.SingleOrDefaultAsync(l => l.Account == account && l.FiscalYear == fiscalYear, ct);

    public async Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default) =>
        await db.BudgetLines.Where(l => l.FiscalYear == fiscalYear).OrderBy(l => l.Account).ToListAsync(ct);

    /// <summary>
    /// Owner of the reservation; the reservation id is the PK of ledger.BudgetReservations.
    /// </summary>
    public Task<BudgetLine?> FindByReservationAsync(Guid reservationId, CancellationToken ct = default) =>
        db.BudgetLines.SingleOrDefaultAsync(l => l.Reservations.Any(r => r.Id == reservationId), ct);

    public async Task AddAsync(BudgetLine line, CancellationToken ct = default) =>
        await db.Set<BudgetLine>().AddAsync(line, ct);
}
