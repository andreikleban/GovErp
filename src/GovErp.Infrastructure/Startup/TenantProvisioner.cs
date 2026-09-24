using System.Globalization;
using GovErp.Application.Web.Common;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Infrastructure.Master;
using GovErp.Infrastructure.Persistence;
using GovErp.Infrastructure.Seed;
using GovErp.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GovErp.Infrastructure.Startup;

public sealed class TenantProvisioner(IOptions<StartupOptions> startup, IOptions<TenancyOptions> tenancy, IClock clock)
{
    /// <summary>The tenant registry is written by the migration user: the Master runtime user only reads.</summary>
    private MasterDbContext MasterForWrite() => new(new DbContextOptionsBuilder<MasterDbContext>()
        .UseSqlServer(string.Format(CultureInfo.InvariantCulture, startup.Value.MigrationConnectionTemplate, startup.Value.MasterDatabase)).Options);

    public async Task ProvisionAsync(string tenantId, string name, string databaseName, string credentialKey, bool isDemo, TenantSeed seed, CancellationToken ct)
    {
        var migration = string.Format(CultureInfo.InvariantCulture, startup.Value.MigrationConnectionTemplate, databaseName);
        await using (var db = new GovErpDbContext(new DbContextOptionsBuilder<GovErpDbContext>().UseSqlServer(migration).Options))
        {
            await db.Database.MigrateAsync(ct);
            await TenantSeeder.SeedAsync(db, seed, ct);
            // An unresolvable set (layer conflict, weakening) throws ValidationException from Resolve; a missing mandatory rule is checked below.
            var effective = RuleResolver.Default.Resolve(await db.RuleDefinitions.ToListAsync(ct), clock.BusinessDate);
            var missing = RuleCatalog.Default.MandatoryRuleIds.Where(id => effective.Find(id) is null).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"Tenant {tenantId} rule set lacks mandatory rules: {string.Join(", ", missing)}");
            }
        }

        var credential = tenancy.Value.Credentials[credentialKey];
        await DatabaseSecurity.EnsureRuntimeUserAsync(string.Format(CultureInfo.InvariantCulture, startup.Value.MigrationConnectionTemplate, "master"),
            databaseName, credential.User, credential.Password, ct);

        await using var master = MasterForWrite();
        if (!await master.Tenants.AnyAsync(t => t.Id == tenantId, ct))
        {
            master.Tenants.Add(new Tenant { Id = tenantId, Name = name, DatabaseName = databaseName, CredentialKey = credentialKey, IsDemo = isDemo });
            await master.SaveChangesAsync(ct);
        }
    }
}
