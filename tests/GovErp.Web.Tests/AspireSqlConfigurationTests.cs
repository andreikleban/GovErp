using System.Globalization;
using GovErp.Web.Extensions;
using Microsoft.Extensions.Configuration;

namespace GovErp.Web.Tests;

public sealed class AspireSqlConfigurationTests
{
    [Fact]
    public void Apply_keeps_braces_in_sa_password_after_format()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:sql"] =
                "Server=127.0.0.1,1433;User ID=sa;Password=p}w{d;TrustServerCertificate=True",
        });

        AspireSqlConfiguration.Apply(configuration);

        var master = string.Format(
            CultureInfo.InvariantCulture,
            configuration["Startup:MigrationConnectionTemplate"]!,
            "master");

        master.Should().Contain("master");
        master.Should().Contain("p}w{d");
    }
}
