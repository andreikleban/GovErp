using GovErp.Domain.Shared.ValueObjects;
using Xunit;

namespace GovErp.Domain.Shared.Tests;

public class AccountCodeTests
{
    [Theory]
    [InlineData("701-6000-53100-G-COPS-26", "G-COPS-26")]
    [InlineData("701-6000-53100", null)]
    public void Parse_round_trips_optional_grant(string text, string? grant)
    {
        var code = AccountCode.Parse(text);
        Assert.Equal("701", code.Fund.Value);
        Assert.Equal("6000", code.Department.Value);
        Assert.Equal("53100", code.Object.Value);
        Assert.Equal(grant, code.Grant?.Value);
        Assert.Equal(text, code.ToString());
        Assert.Equal(code, AccountCode.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("101-6000")]
    [InlineData("101--53100")]
    [InlineData("101-6000-53100-")]
    [InlineData("101-6000-53100-ga")]
    [InlineData("101-6000-53100\n")]
    public void Parse_rejects_invalid_segments(string? text) =>
        Assert.ThrowsAny<ArgumentException>(() => AccountCode.Parse(text!));

    [Fact]
    public void Constructor_rejects_null_required_segments()
    {
        var fund = new FundCode("101");
        var department = new DepartmentCode("6000");
        var obj = new ObjectCode("53100");
        Assert.Throws<ArgumentNullException>(() => new AccountCode(null!, department, obj, null));
        Assert.Throws<ArgumentNullException>(() => new AccountCode(fund, null!, obj, null));
        Assert.Throws<ArgumentNullException>(() => new AccountCode(fund, department, null!, null));
    }

    [Fact]
    public void WithObject_preserves_original_and_other_segments()
    {
        var original = AccountCode.Parse("701-6000-53100-G-COPS-26");
        var changed = original.WithObject(new ObjectCode("2100"));
        Assert.Equal("701-6000-2100-G-COPS-26", changed.ToString());
        Assert.Equal("701-6000-53100-G-COPS-26", original.ToString());
        Assert.NotEqual(original, changed);
        Assert.Throws<ArgumentNullException>(() => original.WithObject(null!));
    }
}
