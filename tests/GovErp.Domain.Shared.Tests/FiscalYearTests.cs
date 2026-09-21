using GovErp.Domain.Shared.ValueObjects;
using Xunit;

namespace GovErp.Domain.Shared.Tests;

public class FiscalYearTests
{
    [Fact]
    public void Fiscal_year_contains_inclusive_july_to_june_dates()
    {
        var year = new FiscalYear(2026);
        Assert.Equal(new DateOnly(2025, 7, 1), year.Start);
        Assert.Equal(new DateOnly(2026, 6, 30), year.End);
        Assert.True(year.Contains(year.Start));
        Assert.True(year.Contains(year.End));
        Assert.False(year.Contains(new DateOnly(2025, 6, 30)));
        Assert.False(year.Contains(new DateOnly(2026, 7, 1)));
        Assert.True(new FiscalYear(2024).Contains(new DateOnly(2024, 2, 29)));
        Assert.Equal("FY2026", year.ToString());
        Assert.Equal(year, new FiscalYear(2026));
    }

    [Theory]
    [InlineData(2025, 7, 1, 2026)]
    [InlineData(2026, 6, 30, 2026)]
    [InlineData(2026, 7, 1, 2027)]
    [InlineData(1, 7, 1, 2)]
    [InlineData(9999, 6, 30, 9999)]
    public void FromDate_uses_july_boundary(int y, int m, int d, int expected) =>
        Assert.Equal(expected, FiscalYear.FromDate(new DateOnly(y, m, d)).Year);

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10000)]
    [InlineData(int.MaxValue)]
    public void Rejects_years_without_representable_start_and_end(int year) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiscalYear(year));

    [Fact]
    public void Rejects_dates_outside_complete_fiscal_year_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FiscalYear.FromDate(DateOnly.MinValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => FiscalYear.FromDate(DateOnly.MaxValue));
        Assert.Equal(new DateOnly(1, 7, 1), new FiscalYear(2).Start);
        Assert.Equal(new DateOnly(9999, 6, 30), new FiscalYear(9999).End);
    }
}
