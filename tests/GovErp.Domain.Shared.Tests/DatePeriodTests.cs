using GovErp.Domain.Shared.ValueObjects;
using Xunit;

namespace GovErp.Domain.Shared.Tests;

public class DatePeriodTests
{
    [Fact]
    public void Closed_period_has_inclusive_boundaries()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var period = new DatePeriod(from, to);
        Assert.Equal(from, period.From);
        Assert.Equal(to, period.To);
        Assert.True(period.Contains(from));
        Assert.True(period.Contains(to));
        Assert.True(period.Contains(new DateOnly(2026, 1, 15)));
        Assert.False(period.Contains(from.AddDays(-1)));
        Assert.False(period.Contains(to.AddDays(1)));
        Assert.True(new DatePeriod(from, from).Contains(from));
        Assert.Equal(period, new DatePeriod(from, to));
    }

    [Fact]
    public void Open_period_has_no_end_but_retains_start()
    {
        var from = new DateOnly(2025, 7, 1);
        var period = new DatePeriod(from, null);
        Assert.Null(period.To);
        Assert.True(period.Contains(from));
        Assert.True(period.Contains(DateOnly.MaxValue));
        Assert.False(period.Contains(from.AddDays(-1)));
    }

    [Fact]
    public void Rejects_reversed_period() =>
        Assert.Throws<ArgumentException>(() => new DatePeriod(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1)));
}
