using GovErp.Domain.Shared.ValueObjects;
using Xunit;

namespace GovErp.Domain.Shared.Tests;

public class IdentifierTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Tenant_rejects_empty_values(string? value) =>
        Assert.ThrowsAny<ArgumentException>(() => new TenantId(value!));

    [Fact]
    public void Identifiers_preserve_identity()
    {
        var tenant = new TenantId("Shelbyville");
        Assert.Equal("Shelbyville", tenant.Value);
        Assert.Equal("Shelbyville", tenant.ToString());
        Assert.Equal(tenant, new TenantId("Shelbyville"));
        Assert.NotEqual(tenant, new TenantId("Springfield"));
        Assert.Throws<InvalidValueException>(() => new UserId(Guid.Empty));
        var user = UserId.New();
        Assert.NotEqual(Guid.Empty, user.Value);
        Assert.Equal(user, new UserId(user.Value));
        Assert.Equal(user.Value.ToString(), user.ToString());
        Assert.NotEqual(user, UserId.New());
    }
}
