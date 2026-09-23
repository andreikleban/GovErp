var builder = DistributedApplication.CreateBuilder(args);

// Единый демо-пароль: SA контейнера SQL Server и runtime-учётки Master и тенантов.
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
