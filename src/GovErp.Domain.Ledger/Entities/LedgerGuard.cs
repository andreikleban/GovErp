using GovErp.Domain.Ledger.Exceptions;
namespace GovErp.Domain.Ledger.Entities;

internal static class LedgerGuard
{
    internal static void Positive(Money amount)
    { if (amount <= Money.Zero) throw new LedgerException("Amount must be positive."); }
    internal static void Owner(Guid invoiceId, int version)
    { if (invoiceId == Guid.Empty || version < 1) throw new LedgerException("Invoice and positive content version are required."); }
}
