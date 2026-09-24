using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// One month of a fiscal year, open or closed for posting.
/// </summary>
public sealed class FiscalPeriod
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public PeriodStatus Status { get; private set; }

    public FiscalPeriod(int year, int month)
    {
        if (month is < 1 or > 12 || year is < 1 or > 9999)
        {
            throw new LedgerException(LedgerErrors.MonthOutOfRange, ("month", month));
        }

        Year = year;
        Month = month;
        Status = PeriodStatus.Open;
    }

    private FiscalPeriod() { }

    public bool IsOpen => Status == PeriodStatus.Open;

    public void Close()
    {
        if (!IsOpen)
        {
            throw new LedgerException(LedgerErrors.PeriodAlreadyClosed, ("period", $"{Year}-{Month:00}"));
        }

        Status = PeriodStatus.Closed;
    }

    public static (int Year, int Month) KeyFor(DateOnly date) => (date.Year, date.Month);
}
