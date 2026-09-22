using GovErp.Application.Web.Budget.Contracts;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;

namespace GovErp.Application.Web.Budget;

public static class BudgetMapping
{
    /// <summary>Opening balance строки — по OpeningBalanceId среди opening balances её финансового года.</summary>
    public static async Task<BudgetLineVm> ToVmAsync(BudgetLine line, IOpeningBalanceRepository openings, CancellationToken ct) =>
        ToVm(line, await openings.ListAsync(line.FiscalYear, ct));

    public static BudgetLineVm ToVm(BudgetLine line, IReadOnlyList<OpeningBalance> openings)
    {
        var opening = line.OpeningBalanceId is { } id ? openings.FirstOrDefault(o => o.Id == id) : null;
        return new BudgetLineVm(line.Account.ToString(), line.FiscalYear.Year, line.ControlMode.ToString(),
            opening?.InitialActuals.Amount ?? 0m, opening?.InitialEncumbered.Amount ?? 0m,
            line.Adopted.Amount, line.Amended.Amount, line.Actuals.Amount, line.Encumbered.Amount, line.Held.Amount, line.Available.Amount,
            line.Amendments.Select(a => new AmendmentVm(a.Amount.Amount, a.Reference, a.EffectiveDate)).ToList());
    }
}
