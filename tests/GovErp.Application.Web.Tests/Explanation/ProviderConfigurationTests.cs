using GovErp.Application.Web.Explanation;
using GovErp.Infrastructure.Explanation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Tests.Explanation;

/// <summary>Регистрация клиентов проверяется без сетевого вызова: фабрика только конструирует клиент.</summary>
public class ProviderConfigurationTests
{
    [Fact]
    public void Template_does_not_register_a_chat_client()
    {
        using var provider = Build(("Explanation:Provider", "Template"));
        provider.GetService<IChatClient>().Should().BeNull();
        provider.GetRequiredService<IExplanationGenerator>().Should().BeOfType<LlmExplanationGenerator>();
    }

    [Fact]
    public void Unknown_provider_fails_at_registration()
    {
        var act = () => Build(("Explanation:Provider", "Gemini"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*Gemini*");
    }

    [Fact]
    public void OpenAi_without_a_key_does_not_construct_a_client()
    {
        using var provider = Build(("Explanation:Provider", "OpenAI"));
        provider.GetService<IChatClient>().Should().BeNull();
    }

    [Fact]
    public void Explanation_provider_environment_name_selects_OpenAi_over_the_template_default()
    {
        using var provider = Build(
            ("Explanation:Provider", "Template"),
            ("EXPLANATION_PROVIDER", "OpenAI"),
            ("OPENAI_API_KEY", "test-key"));
        provider.GetRequiredService<IChatClient>().GetType().FullName.Should().Contain("OpenAI");
    }

    [Fact]
    public void OpenAi_with_a_key_registers_a_client_without_calling_it()
    {
        using var provider = Build(
            ("Explanation:Provider", "OpenAI"),
            ("Explanation:ApiKey", "test-key"),
            ("Explanation:Model", "gpt-5.1"));
        var client = provider.GetRequiredService<IChatClient>();
        client.GetType().FullName.Should().Contain("OpenAI");
    }

    [Fact]
    public void OpenAi_rejects_an_endpoint_that_is_not_a_uri()
    {
        var act = () => Build(
            ("Explanation:Provider", "OpenAI"),
            ("Explanation:ApiKey", "test-key"),
            ("Explanation:Endpoint", "not a uri"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*Endpoint*");
    }

    [Fact]
    public void Anthropic_without_a_key_does_not_construct_a_client()
    {
        using var provider = Build(("Explanation:Provider", "Anthropic"));
        provider.GetService<IChatClient>().Should().BeNull();
    }

    [Fact]
    public void Anthropic_with_a_key_registers_a_client_without_calling_it()
    {
        using var provider = Build(
            ("Explanation:Provider", "Anthropic"),
            ("ANTHROPIC_API_KEY", "test-key"));
        provider.GetRequiredService<IChatClient>().GetType().FullName.Should().Contain("Anthropic");
    }

    [Fact]
    public void Ollama_without_an_endpoint_does_not_construct_a_client()
    {
        using var provider = Build(("Explanation:Provider", "Ollama"), ("Explanation:Model", "demo"));
        provider.GetService<IChatClient>().Should().BeNull();
    }

    [Fact]
    public void Ollama_with_an_endpoint_and_model_registers_a_client_without_calling_it()
    {
        using var provider = Build(
            ("Explanation:Provider", "Ollama"),
            ("Explanation:Endpoint", "http://127.0.0.1:9"),
            ("Explanation:Model", "demo"));
        provider.GetRequiredService<IChatClient>().GetType().FullName.Should().Contain("Ollama");
    }

    private static ServiceProvider Build(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(item => item.Key, item => (string?)item.Value)).Build();
        return new ServiceCollection().AddExplanation(configuration).BuildServiceProvider();
    }
}
