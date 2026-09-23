using GovErp.Application.Web.Budget.Contracts;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;

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

    /// <summary>Резервы строки с номером инвойса, который держит (или держал) каждый; инвойс мог быть уже удалён из вида — тогда id как заглушка.</summary>
    public static async Task<IReadOnlyList<BudgetReservationVm>> ReservationsAsync(BudgetLine line, IVendorInvoiceRepository invoices, CancellationToken ct)
    {
        var result = new List<BudgetReservationVm>();
        foreach (var r in line.Reservations)
        {
            var reference = (await invoices.FindAsync(r.InvoiceId, ct))?.Reference ?? r.InvoiceId.ToString();
            result.Add(new BudgetReservationVm(r.InvoiceId, reference, r.Amount.Amount, r.Status.ToString()));
        }

        return result;
    }

    /// <summary>Encumbrance с claims ликвидации и billing claims, у каждого — номер инвойса вместо голого id.</summary>
    public static async Task<EncumbranceVm> ToEncumbranceVmAsync(Encumbrance encumbrance, IVendorInvoiceRepository invoices, CancellationToken ct)
    {
        var claims = new List<EncumbranceClaimVm>();
        foreach (var c in encumbrance.Claims)
        {
            claims.Add(new EncumbranceClaimVm(c.InvoiceId, (await invoices.FindAsync(c.InvoiceId, ct))?.Reference ?? c.InvoiceId.ToString(),
                c.Amount.Amount, c.Status.ToString()));
        }

        var billing = new List<EncumbranceClaimVm>();
        foreach (var c in encumbrance.BillingClaims)
        {
            billing.Add(new EncumbranceClaimVm(c.InvoiceId, (await invoices.FindAsync(c.InvoiceId, ct))?.Reference ?? c.InvoiceId.ToString(),
                c.Amount.Amount, c.Status.ToString()));
        }

        return new EncumbranceVm(encumbrance.PoLineRef, encumbrance.Account.ToString(), encumbrance.Status.ToString(),
            encumbrance.Original.Amount, encumbrance.Liquidated.Amount, encumbrance.Released.Amount, encumbrance.Remaining.Amount,
            encumbrance.AuthorizedPoAmount.Amount, encumbrance.AlreadyPostedAgainstPo.Amount, claims, billing);
    }
}
