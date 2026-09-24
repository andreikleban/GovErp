using GovErp.Application.Web.Extensions;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;
using GovErp.Infrastructure.Extensions;
using GovErp.Infrastructure.Persistence.Repositories;
using GovErp.Infrastructure.Seed;
using GovErp.Infrastructure.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace GovErp.Application.Web.Tests;

[CollectionDefinition("sql")]
public sealed class SqlCollection : ICollectionFixture<SqlServerFixture>;

/// <summary>
/// One SQL Server for the "sql" collection: Master and the startup pipeline as in the Web host, repositories wrapped with TestHooks.
/// Every test gets its own tenant database (consistency §7: tests do not share mutable balances).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";
    private const string RuntimeUser = "goverp_test";
    private const string DemoPassword = "1!Qwertyui";
    private readonly MsSqlContainer _sql = new MsSqlBuilder(Image).WithPassword(DemoPassword).Build();

    private AsyncServiceScope _appScope;

    public ServiceProvider Services { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;

    /// <summary>
    /// App services are registered as scoped (validateScopes forbids resolving them from the root); they are stateless,
    /// and every operation runs in its own runner scope anyway, so tests take them from one long-lived scope.
    /// </summary>
    public IServiceProvider App => _appScope.ServiceProvider;

    public TestHooks Hooks { get; } = new();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        var sa = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sql.GetConnectionString());
        string Template(string user, string password) =>
            $"Server={sa.DataSource};Database={{0}};User Id={user};Password={password};TrustServerCertificate=True";

        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Startup:MigrationConnectionTemplate"] = Template(sa.UserID, sa.Password),
            ["Startup:MasterDatabase"] = "GovErp_Master",
            ["Startup:MasterRuntimeLogin"] = "goverp_master_ro",
            ["Startup:MasterRuntimePassword"] = "1!Qwertyui",
            ["Startup:DemoResetAllowlist:0"] = "GovErp_Springfield",
            ["ConnectionStrings:Master"] = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                Template("goverp_master_ro", "1!Qwertyui"), "GovErp_Master"),
            ["Tenancy:RuntimeConnectionTemplate"] = $"Server={sa.DataSource};Database={{0}};User Id={{1}};Password={{2}};TrustServerCertificate=True",
            ["Tenancy:Credentials:springfield:User"] = "goverp_springfield", ["Tenancy:Credentials:springfield:Password"] = "1!Qwertyui",
            ["Tenancy:Credentials:shelbyville:User"] = "goverp_shelbyville", ["Tenancy:Credentials:shelbyville:Password"] = "1!Qwertyui",
            ["Tenancy:Credentials:test:User"] = RuntimeUser, ["Tenancy:Credentials:test:Password"] = DemoPassword,
            ["Clock:BusinessDate"] = "2026-07-15",
        }).Build();
        Configuration = cfg;

        var services = new ServiceCollection().AddLogging();
        services.AddApplication(cfg).AddInfrastructure(cfg).AddStartup(cfg);
        services.AddSingleton(Hooks);
        services.AddScoped<EfBudgetLineRepository>().AddScoped<IBudgetLineRepository>(sp =>
            new HookedBudgetLineRepository(sp.GetRequiredService<EfBudgetLineRepository>(), Hooks));
        services.AddScoped<EfEncumbranceRepository>().AddScoped<IEncumbranceRepository>(sp =>
            new HookedEncumbranceRepository(sp.GetRequiredService<EfEncumbranceRepository>(), Hooks));
        services.AddScoped<EfVendorInvoiceRepository>().AddScoped<IVendorInvoiceRepository>(sp =>
            new HookedVendorInvoiceRepository(sp.GetRequiredService<EfVendorInvoiceRepository>(), Hooks));
        services.AddScoped<EfJournalRepository>().AddScoped<IJournalRepository>(sp =>
            new HookedJournalRepository(sp.GetRequiredService<EfJournalRepository>(), Hooks));
        Services = services.BuildServiceProvider(validateScopes: true);
        _appScope = Services.CreateAsyncScope();
        await DatabaseInitializer.InitializeAsync(Services, CancellationToken.None);
    }

    public async Task<TenantDriver> CreateTenantAsync()
    {
        Hooks.Reset();
        var id = $"t{Guid.NewGuid():N}"[..20];
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TenantProvisioner>()
            .ProvisionAsync(id, $"Test {id}", $"GovErp_T_{id}", "test", isDemo: false, TenantSeed.Springfield, CancellationToken.None);
        return new TenantDriver(this, new TenantId(id));
    }

    /// <summary>
    /// Tenants from DatabaseInitializer (springfield / shelbyville). Needed for Users and actor names:
    /// Master stores users only under these TenantIds; CreateTenantAsync creates a separate database without rows in Master.
    /// </summary>
    public TenantDriver Named(string tenantId) => new(this, new TenantId(tenantId));

    public async Task DisposeAsync()
    {
        await _appScope.DisposeAsync();
        await Services.DisposeAsync();
        await _sql.DisposeAsync();
    }
}
