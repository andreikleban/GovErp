using GovErp.Application.Web.Explanation;
using GovErp.Infrastructure.Explanation.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Infrastructure.Explanation;

public static class ExplanationRegistration
{
    public static IServiceCollection AddExplanation(this IServiceCollection services, IConfiguration configuration)
    {
        var options = ExplanationConfiguration.Read(configuration);
        services.Configure<ExplanationOptions>(configuration.GetSection("Explanation"));
        switch (options.Provider)
        {
            case ExplanationConfiguration.Template:
                break;
            case ExplanationConfiguration.OpenAi:
                OpenAiRegistration.Add(services, options, configuration);
                break;
            case ExplanationConfiguration.Anthropic:
                AnthropicRegistration.Add(services, options, configuration);
                break;
            case ExplanationConfiguration.Ollama:
                OllamaRegistration.Add(services, options);
                break;
            default:
                throw new InvalidOperationException($"Unknown explanation provider '{options.Provider}'.");
        }

        services.AddSingleton<TemplateExplanationGenerator>();
        services.AddSingleton<IExplanationGenerator>(sp => new LlmExplanationGenerator(
            sp.GetRequiredService<TemplateExplanationGenerator>(),
            options,
            sp.GetService<IChatClient>()));
        return services;
    }
}
