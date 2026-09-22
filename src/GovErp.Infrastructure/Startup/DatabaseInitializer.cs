using System.Globalization;
using GovErp.Infrastructure.Master;
using GovErp.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GovErp.Infrastructure.Startup;

/// <summary>Порядок старта: ждать SQL, мигрировать Master, завести read-only master-пользователя, засеять Master, провижинить тенантов.</summary>
public static class DatabaseInitializer
{
    private const int MaxAttempts = 20;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        var startup = services.GetRequiredService<IOptions<StartupOptions>>().Value;
        var masterConnection = string.Format(CultureInfo.InvariantCulture, startup.MigrationConnectionTemplate, "master");

        // SQL в контейнере стартует дольше приложения: до MaxAttempts попыток с паузой RetryDelay.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var probe = new SqlConnection(masterConnection);
                await probe.OpenAsync(ct);
                break;
            }
            catch (SqlException) when (attempt < MaxAttempts)
            {
                await Task.Delay(RetryDelay, ct);
            }
        }

        var masterMigration = string.Format(CultureInfo.InvariantCulture, startup.MigrationConnectionTemplate, startup.MasterDatabase);
        await using (var master = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(masterMigration).Options))
        {
            await master.Database.MigrateAsync(ct);
        }

        await DatabaseSecurity.EnsureReadOnlyUserAsync(masterConnection, startup.MasterDatabase, startup.MasterRuntimeLogin, startup.MasterRuntimePassword, ct);

        await using (var master = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(masterMigration).Options))
        {
            var hasher = services.GetRequiredService<IPasswordHasher<UserAccount>>();
            await MasterSeed.SeedUsersAsync(master, hasher, ct);
        }

        var provisioner = services.GetRequiredService<TenantProvisioner>();
        await provisioner.ProvisionAsync("springfield", "City of Springfield", "GovErp_Springfield", "springfield", isDemo: true, TenantSeed.Springfield, ct);
        await provisioner.ProvisionAsync("shelbyville", "City of Shelbyville", "GovErp_Shelbyville", "shelbyville", isDemo: false, TenantSeed.ReferenceOnly, ct);
    }
}
