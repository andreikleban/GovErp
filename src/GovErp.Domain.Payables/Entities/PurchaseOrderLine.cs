using GovErp.Domain.Payables.Exceptions;
namespace GovErp.Domain.Payables.Entities;

public sealed record PurchaseOrderLine
{
    public int LineNo { get; }
    public AccountCode Account { get; }
    public Money Amount { get; }
    public PurchaseOrderLine(int lineNo, AccountCode account, Money amount)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (lineNo < 1 || amount <= Money.Zero) throw new PayablesException("PO line number and amount must be positive.");
        LineNo = lineNo; Account = account; Amount = amount;
    }
}
