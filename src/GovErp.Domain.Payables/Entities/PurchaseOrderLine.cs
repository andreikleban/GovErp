using GovErp.Domain.Payables.Exceptions;
namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// A purchase-order line: its account and amount.
/// </summary>
public sealed record PurchaseOrderLine
{
    public int LineNo { get; }
    public AccountCode Account { get; }
    public Money Amount { get; }
    public PurchaseOrderLine(int lineNo, AccountCode account, Money amount)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (lineNo < 1 || amount <= Money.Zero) throw new PayablesException(PayablesErrors.PoLineNotPositive);
        LineNo = lineNo; Account = account; Amount = amount;
    }
}
