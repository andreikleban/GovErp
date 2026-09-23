using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace GovErp.Infrastructure.Explanation.Providers;

public static class OpenAiRegistration
{
    public static void Add(IServiceCollection services, ExplanationOptions options, IConfiguration configuration)
    {
        var key = ExplanationConfiguration.ApiKey(configuration, "OPENAI_API_KEY");
        if (key is null)
        {
            return;
        }

        var model = ExplanationConfiguration.Model(options);
        var endpoint = ExplanationConfiguration.Endpoint(options.Endpoint);
        services.AddChatClient(_ =>
        {
            var client = endpoint is null
                ? new ChatClient(model, key)
                : new ChatClient(model, new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint });
            return client.AsIChatClient();
        });
    }
}
