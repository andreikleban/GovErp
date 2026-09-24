namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>
/// A fiscal year running from 1 July of the previous calendar year through 30 June.
/// </summary>
public readonly record struct FiscalYear
{
    public int Year { get; }
    public DateOnly Start => new(Year - 1, 7, 1);
    public DateOnly End => new(Year, 6, 30);
    public FiscalYear(int year)
    {
        if (year is < 2 or > 9999) throw new InvalidValueException(nameof(year), ValueErrors.FiscalYearRange, ("year", year));
        Year = year;
    }
    public bool Contains(DateOnly date) => date >= Start && date <= End;
    public static FiscalYear FromDate(DateOnly date) => new(date.Month >= 7 ? date.Year + 1 : date.Year);
    public override string ToString() => $"FY{Year}";
}
