using GovErp.Domain.Shared.ValueObjects;
using Xunit;

namespace GovErp.Domain.Shared.Tests;

public class SegmentCodeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("101\n")]
    [InlineData("１２３")]
    [InlineData("70A")]
    [InlineData("10")]
    [InlineData("1010")]
    public void Fund_rejects_invalid_codes(string? value) =>
        Assert.ThrowsAny<ArgumentException>(() => new FundCode(value!));

    [Fact]
    public void Codes_preserve_text_and_validate_their_own_format()
    {
        Assert.Equal("101", new FundCode("101").ToString());
        Assert.Equal("0000", new DepartmentCode("0000").Value);
        Assert.Equal("2100", new ObjectCode("2100").Value);
        Assert.Equal("53100", new ObjectCode("53100").ToString());
        Assert.Equal("G-COPS-26", new GrantCode("G-COPS-26").ToString());
        foreach (var value in new string?[] { null, "", "600", "60000", "6000\n", "60A0" })
            Assert.ThrowsAny<ArgumentException>(() => new DepartmentCode(value!));
        foreach (var value in new string?[] { null, "", "210", "531000", "53100\n", "53A00" })
            Assert.ThrowsAny<ArgumentException>(() => new ObjectCode(value!));
        foreach (var value in new string?[] { null, "", "G", "g cops", "1A", "GA\n", new string('A', 32) })
            Assert.ThrowsAny<ArgumentException>(() => new GrantCode(value!));
        Assert.Equal(new string('A', 31), new GrantCode(new string('A', 31)).Value);
        Assert.Equal(new ObjectCode("2100"), new ObjectCode("2100"));
        Assert.False(new ObjectCode("2100").Equals((object)new DepartmentCode("2100")));
    }
}
