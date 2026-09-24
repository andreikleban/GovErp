using GovErp.Domain.Ledger.Exceptions;
namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// Shared checks of amounts and invoice version inside the ledger.
/// </summary>
internal static class LedgerGuard
{
    internal static void Positive(Money amount)
    { if (amount <= Money.Zero) throw new LedgerException(LedgerErrors.AmountNotPositive, ("amount", amount)); }
    internal static void Owner(Guid invoiceId, int version)
    { if (invoiceId == Guid.Empty || version < 1) throw new LedgerException(LedgerErrors.InvoiceVersionRequired); }
}
