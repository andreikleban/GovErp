using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Web.Extensions;

/// <summary>
/// Точка расширения для Web-специфичных сервисов (аутентификация, авторизация Blazor — план 4).
/// Заведена в задаче 7 вместе с Dockerfile/compose, чтобы каталог уже существовал: Web ссылается
/// на Application.Web и Infrastructure только из Program.cs и Extensions/ (global constraint).
/// Пока пуста — план 4 добавит сюда регистрацию аутентификации и вызовет из Program.cs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebHost(this IServiceCollection services, IConfiguration configuration) => services;
}
