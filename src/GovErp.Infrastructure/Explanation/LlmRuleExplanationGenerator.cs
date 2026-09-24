using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Reference.Contracts;
using Microsoft.Extensions.AI;

namespace GovErp.Infrastructure.Explanation;

/// <summary>
/// The same provider, timeout and limits as the evaluation explanation. An empty or too long answer, a timeout and an error
/// return the written description with a reason; cancelling the caller's token is not replaced by the template.
/// </summary>
public sealed class LlmRuleExplanationGenerator(ExplanationOptions options, IChatClient? chat = null) : IRuleExplanationGenerator
{
    public async Task<ExplanationResult> ExplainAsync(RuleDescriptionVm description, RuleVm? current, ExplanationAudience audience,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (options.Provider == ExplanationConfiguration.Template)
        {
            return TemplateRuleExplanation.Create(description, current, audience);
        }

        if (chat is null)
        {
            return TemplateRuleExplanation.Create(description, current, audience, ExplanationConfiguration.Unavailable(options));
        }

        var model = ExplanationConfiguration.Model(options);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            var response = await chat.GetResponseAsync(RuleExplanationPrompt.Create(description, current, audience),
                new ChatOptions { ModelId = model, ToolMode = ChatToolMode.None }, linked.Token);
            var text = response.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return TemplateRuleExplanation.Create(description, current, audience, "The model returned an empty explanation.");
            }

            return text.Length > options.MaxOutputCharacters
                ? TemplateRuleExplanation.Create(description, current, audience, $"The model response exceeded {options.MaxOutputCharacters} characters.")
                : new ExplanationResult(text, options.Provider, model, RuleExplanationPrompt.Version, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return TemplateRuleExplanation.Create(description, current, audience, $"The model did not respond within {options.TimeoutSeconds} seconds.");
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return TemplateRuleExplanation.Create(description, current, audience, $"{options.Provider} request failed.");
        }
    }
}
