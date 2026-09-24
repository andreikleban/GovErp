var builder = DistributedApplication.CreateBuilder(args);

// Single demo password: SA of the SQL Server container and the runtime logins of Master and the tenants.
const string DemoPassword = "1!Qwertyui";
var sqlPassword = builder.AddParameter("sql-password", DemoPassword, secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("goverp-sql");

var web = builder.AddProject<Projects.GovErp_Web>("web")
    .WithReference(sql)
    .WaitFor(sql)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Demo")
    .WithEnvironment("Startup__MasterRuntimePassword", DemoPassword)
    .WithEnvironment("Tenancy__Credentials__springfield__Password", DemoPassword)
    .WithEnvironment("Tenancy__Credentials__shelbyville__Password", DemoPassword);

// The AppHost launchSettings do not reach the Web process. Provider and key are read from the AppHost configuration
// (user-secrets or environment variables) and passed to the site separately.
Pass("Explanation__Provider", "EXPLANATION_PROVIDER", "Explanation:Provider");
Pass("Explanation__Model", "EXPLANATION_MODEL", "Explanation:Model");
Pass("Explanation__Endpoint", "EXPLANATION_ENDPOINT", "Explanation:Endpoint");
Pass("Explanation__ApiKey", "Explanation:ApiKey");
Pass("OPENAI_API_KEY", "OPENAI_API_KEY");
Pass("ANTHROPIC_API_KEY", "ANTHROPIC_API_KEY");

builder.Build().Run();

void Pass(string destination, params string[] sources)
{
    foreach (var source in sources)
    {
        var value = builder.Configuration[source];
        if (string.IsNullOrWhiteSpace(value))
        {
            continue;
        }

        web = web.WithEnvironment(destination, value);
        return;
    }
}
