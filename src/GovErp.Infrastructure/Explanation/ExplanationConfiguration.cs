using Microsoft.Extensions.Configuration;

namespace GovErp.Infrastructure.Explanation;

/// <summary>
/// Provider names and settings. The key comes from the host configuration (including environment variables)
/// and never reaches the log or the prompt text.
/// </summary>
internal static class ExplanationConfiguration
{
    public const string Template = "Template";
    public const string OpenAi = "OpenAI";
    public const string Anthropic = "Anthropic";
    public const string Ollama = "Ollama";

    /// <summary>Model from the official OpenAI .NET 2.13 sample (overridden by Explanation:Model).</summary>
    public const string OpenAiModel = "gpt-5.1";

    /// <summary>Model from the official Anthropic SDK 12.50 sample (overridden by Explanation:Model).</summary>
    public const string AnthropicModel = "claude-sonnet-4-5";

    public const int DefaultMaxOutputTokens = 2048;

    public static ExplanationOptions Read(IConfiguration configuration)
    {
        var options = configuration.GetSection("Explanation").Get<ExplanationOptions>() ?? new ExplanationOptions();
        options.Provider = First(configuration["EXPLANATION_PROVIDER"], options.Provider, Template);
        options.Model = First(configuration["EXPLANATION_MODEL"], options.Model, "");
        options.Endpoint = First(configuration["EXPLANATION_ENDPOINT"], options.Endpoint, "");
        if (options.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Explanation:TimeoutSeconds must be positive.");
        }

        if (options.MaxOutputCharacters <= 0)
        {
            throw new InvalidOperationException("Explanation:MaxOutputCharacters must be positive.");
        }

        return options;
    }

    public static string Model(ExplanationOptions options) => options.Provider switch
    {
        OpenAi => Or(options.Model, OpenAiModel),
        Anthropic => Or(options.Model, AnthropicModel),
        _ => options.Model,
    };

    /// <summary>Explanation:ApiKey first, then the provider's standard variable. An empty string means no key.</summary>
    public static string? ApiKey(IConfiguration configuration, string environmentName)
    {
        var dedicated = configuration["Explanation:ApiKey"];
        if (!string.IsNullOrWhiteSpace(dedicated))
        {
            return dedicated;
        }

        var fromEnvironment = configuration[environmentName];
        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment;
    }

    public static Uri? Endpoint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Explanation:Endpoint is not an absolute URI.");
        }

        return uri;
    }

    public static string Unavailable(ExplanationOptions options) => options.Provider switch
    {
        OpenAi => "OpenAI API key is not configured.",
        Anthropic => "Anthropic API key is not configured.",
        Ollama when string.IsNullOrWhiteSpace(options.Endpoint) => "Ollama endpoint is not configured.",
        Ollama => "Ollama model is not configured.",
        _ => $"{options.Provider} client is not configured.",
    };

    private static string Or(string configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured;

    private static string First(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }
}
