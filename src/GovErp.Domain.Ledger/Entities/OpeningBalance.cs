using GovErp.Domain.Ledger.Exceptions;
namespace GovErp.Domain.Ledger.Entities;

public sealed class OpeningBalance
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public AccountCode Account { get; private set; }
    public FiscalYear FiscalYear { get; private set; }
    public DateOnly AsOfDate { get; private set; }
    public Money InitialActuals { get; private set; }
    public Money InitialEncumbered { get; private set; }
    public string SourceReference { get; private set; }
    private OpeningBalance() { Account = null!; SourceReference = null!; }
    public OpeningBalance(AccountCode account, FiscalYear fiscalYear, DateOnly asOfDate, Money initialActuals, Money initialEncumbered, string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(account); ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        if (fiscalYear.Year is < 2 or > 9999 || !fiscalYear.Contains(asOfDate)) throw new LedgerException("Opening date must belong to the budget year.");
        if (initialActuals.IsNegative || initialEncumbered.IsNegative) throw new LedgerException("Opening balances cannot be negative.");
        Account = account; FiscalYear = fiscalYear; AsOfDate = asOfDate; InitialActuals = initialActuals; InitialEncumbered = initialEncumbered; SourceReference = sourceReference;
    }
}
