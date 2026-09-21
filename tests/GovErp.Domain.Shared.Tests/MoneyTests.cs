using GovErp.Domain.Shared.ValueObjects;
using Xunit;

namespace GovErp.Domain.Shared.Tests;

public class MoneyTests
{
    [Fact]
    public void Rejects_fractional_cents_instead_of_rounding()
    {
        foreach (var amount in new[] { 1.005m, 1.015m, -0.001m, 0.0000000000000000000000000001m })
        {
            Assert.Throws<ArgumentException>(() => Money.Of(amount));
            Assert.Throws<ArgumentException>(() => new Money(amount));
        }
    }

    [Fact]
    public void Enforces_sql_decimal_range()
    {
        const decimal limit = 9999999999999999.99m;
        Assert.Equal(limit, Money.Of(limit).Amount);
        Assert.Equal(-limit, Money.Of(-limit).Amount);
        foreach (var amount in new[] { 10000000000000000m, -10000000000000000m, decimal.MaxValue, decimal.MinValue })
            Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(amount));
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(limit) + Money.Of(.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(-limit) - Money.Of(.01m));
    }

    [Fact]
    public void Arithmetic_preserves_signed_values()
    {
        var a = Money.Of(160000m);
        var b = Money.Of(147000m);
        Assert.Equal(13000m, (a - b).Amount);
        Assert.Equal(-13000m, (b - a).Amount);
        Assert.Equal(307000m, (a + b).Amount);
        Assert.Equal(-160000m, (-a).Amount);
        Assert.True((-a).IsNegative);
        Assert.False(a.IsNegative);
        Assert.Equal(160000m, a.Amount);
    }

    [Fact]
    public void Comparison_equality_and_formatting_use_amount()
    {
        var one = Money.Of(1m);
        var two = Money.Of(2m);
        Assert.True(one < two);
        Assert.True(two > one);
        Assert.True(one <= Money.Of(1));
        Assert.True(two >= Money.Of(2));
        Assert.False(two < one);
        Assert.False(one > two);
        Assert.Equal(one, Money.Min(one, two));
        Assert.Equal(one, Money.Min(two, one));
        Assert.Equal(two, Money.Max(one, two));
        Assert.Equal(two, Money.Max(two, one));
        Assert.True(one.CompareTo(two) < 0);
        Assert.Equal(0, one.CompareTo(Money.Of(1.00m)));
        Assert.Equal(Money.Of(10), Money.Of(10.000m));
        Assert.NotEqual(Money.Of(10), Money.Of(10.01m));
        Assert.True(Money.Zero.IsZero);
        Assert.False(one.IsZero);
        Assert.Equal(Money.Zero, default(Money));
        Assert.Equal("1,234.50", Money.Of(1234.5m).ToString());
    }
}
