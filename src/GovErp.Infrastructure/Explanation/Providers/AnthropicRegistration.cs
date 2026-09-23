using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Infrastructure.Explanation.Providers;

public static class AnthropicRegistration
{
    public static void Add(IServiceCollection services, ExplanationOptions options, IConfiguration configuration)
    {
        var key = ExplanationConfiguration.ApiKey(configuration, "ANTHROPIC_API_KEY");
        if (key is null)
        {
            return;
        }

        var model = ExplanationConfiguration.Model(options);
        var endpoint = ExplanationConfiguration.Endpoint(options.Endpoint);
        services.AddChatClient(_ =>
        {
            var settings = new Anthropic.Core.ClientOptions { ApiKey = key };
            if (endpoint is not null)
            {
                settings.BaseUrl = endpoint.AbsoluteUri;
            }

            return new AnthropicClient(settings).AsIChatClient(model, defaultMaxOutputTokens: ExplanationConfiguration.DefaultMaxOutputTokens);
        });
    }
}
