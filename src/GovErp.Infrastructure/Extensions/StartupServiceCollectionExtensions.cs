using GovErp.Infrastructure.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Infrastructure.Extensions;

/// <summary>
/// Registers the startup pipeline (provisioning, database initialization, demo-reset). Separate from AddInfrastructure:
/// it uses the migration credentials (the "Startup" section), which the runtime DI graph must not have.
/// </summary>
public static class StartupServiceCollectionExtensions
{
    public static IServiceCollection AddStartup(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<StartupOptions>(cfg.GetSection("Startup"));
        // No scoped dependencies: it opens its own connections from MigrationConnectionTemplate, so it is safe as a singleton.
        s.AddSingleton<TenantProvisioner>();
        return s;
    }
}
