using System.Globalization;
using GovErp.Infrastructure.Master;
using GovErp.Infrastructure.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GovErp.Infrastructure.Startup;

public static class DemoReset
{
    /// <summary>demo-reset --tenant springfield --confirm springfield. Только Environment=Demo, IsDemo, имя БД в allowlist.</summary>
    public static async Task<int> RunAsync(string[] args, IServiceProvider services, IHostEnvironment env, CancellationToken ct)
    {
        string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
        var tenantId = Arg("--tenant");
        if (!env.IsEnvironment("Demo") || tenantId is null || Arg("--confirm") != tenantId)
        {
            Console.Error.WriteLine("demo-reset requires Environment=Demo and --tenant X --confirm X.");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var startup = scope.ServiceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
        var master = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var tenant = await master.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || !tenant.IsDemo || !startup.DemoResetAllowlist.Contains(tenant.DatabaseName))
        {
            Console.Error.WriteLine($"Tenant '{tenantId}' is not a resettable demo tenant.");
            return 3;
        }

        Console.WriteLine($"Resetting demo tenant {tenant.Id} ({tenant.DatabaseName}). All its invoices, evaluations and audit history will be deleted.");
        var migration = string.Format(CultureInfo.InvariantCulture, startup.MigrationConnectionTemplate, "master");
        await using (var connection = new SqlConnection(migration))
        {
            await connection.OpenAsync(ct);
            // Имя БД прошло allowlist; SINGLE_USER закрывает активные операции на время сброса.
            await using var drop = new SqlCommand(
                $"IF DB_ID(N'{tenant.DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{tenant.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{tenant.DatabaseName}]; END",
                connection);
            await drop.ExecuteNonQueryAsync(ct);
        }

        await scope.ServiceProvider.GetRequiredService<TenantProvisioner>()
            .ProvisionAsync(tenant.Id, tenant.Name, tenant.DatabaseName, tenant.CredentialKey, tenant.IsDemo, TenantSeed.Springfield, ct);
        Console.WriteLine("Demo tenant reset complete.");
        return 0;
    }
}
