using GovErp.Infrastructure.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Infrastructure.Extensions;

/// <summary>
/// Регистрация startup-конвейера (провижининг, инициализация БД, demo-reset). Отдельно от AddInfrastructure:
/// использует migration-учётные данные (секция "Startup"), которых у runtime DI-графа быть не должно.
/// </summary>
public static class StartupServiceCollectionExtensions
{
    public static IServiceCollection AddStartup(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<StartupOptions>(cfg.GetSection("Startup"));
        // Без scoped-зависимостей: сам открывает соединения по MigrationConnectionTemplate — безопасен как singleton.
        s.AddSingleton<TenantProvisioner>();
        return s;
    }
}
