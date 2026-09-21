using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

public sealed class FiscalPeriod
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public PeriodStatus Status { get; private set; }

    public FiscalPeriod(int year, int month)
    {
        if (month is < 1 or > 12 || year is < 1 or > 9999)
        {
            throw new LedgerException($"Month {month} is out of range.");
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
            throw new LedgerException($"Period {Year}-{Month:00} is already closed.");
        }

        Status = PeriodStatus.Closed;
    }

    public static (int Year, int Month) KeyFor(DateOnly date) => (date.Year, date.Month);
}
