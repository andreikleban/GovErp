using GovErp.Application.Web.Ledger.Contracts;
using GovErp.Domain.Ledger.Entities;

namespace GovErp.Application.Web.Ledger;

/// <summary>
/// Builds journal lines and fiscal periods for the screen.
/// </summary>
public static class LedgerMapping
{
    /// <summary>Balance per (fund, family): JournalEntry.Create already guarantees equal debits and credits in each group (a GE invariant);
    /// here it is only displayed, for demo transparency, not as a separate check.</summary>
    public static JournalEntryVm ToVm(JournalEntry entry, Guid? invoiceId, string? invoiceReference)
    {
        var lines = entry.Lines.Select(l => new JournalLineVm(l.Account.ToString(), l.Family.ToString(), l.Debit.Amount, l.Credit.Amount, l.Description)).ToList();
        var balances = entry.Lines.GroupBy(l => (Fund: l.Account.Fund.Value, Family: l.Family.ToString()))
            .Select(g => new FundFamilyBalanceVm(g.Key.Fund, g.Key.Family, g.Sum(l => l.Debit.Amount), g.Sum(l => l.Credit.Amount)))
            .OrderBy(b => b.Fund, StringComparer.Ordinal).ThenBy(b => b.Family, StringComparer.Ordinal).ToList();
        return new JournalEntryVm(entry.Id, entry.SourceRef, invoiceId, invoiceReference, entry.PeriodYear, entry.PeriodMonth, entry.PostedAt, lines, balances);
    }
}
