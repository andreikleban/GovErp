using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace GovErp.Infrastructure.Explanation.Providers;

/// <summary>
/// Registers Ollama as an explanation provider.
/// </summary>
public static class OllamaRegistration
{
    public static void Add(IServiceCollection services, ExplanationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.Model))
        {
            return;
        }

        var endpoint = ExplanationConfiguration.Endpoint(options.Endpoint)
            ?? throw new InvalidOperationException("Ollama endpoint is not configured.");
        var model = options.Model;
        services.AddChatClient(_ => new OllamaApiClient(endpoint, model));
    }
}
