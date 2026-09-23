var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("goverp-sql");

var web = builder.AddProject<Projects.GovErp_Web>("web")
    .WithReference(sql)
    .WaitFor(sql)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Demo")
    .WithEnvironment("Startup__MasterRuntimePassword", "GovErp!MasterRo2026")
    .WithEnvironment("Tenancy__Credentials__springfield__Password", "GovErp!Springfield2026")
    .WithEnvironment("Tenancy__Credentials__shelbyville__Password", "GovErp!Shelbyville2026");

// launchSettings AppHost не попадает в процесс Web. Провайдер и ключ читаются из конфигурации AppHost
// (user-secrets или переменные окружения) и передаются сайту отдельно.
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
