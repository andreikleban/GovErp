using GovErp.Application.Web.Explanation;
using GovErp.Domain.Validation.Entities;
using Microsoft.Extensions.AI;

namespace GovErp.Infrastructure.Explanation;

/// <summary>
/// Calls IChatClient without tools. An empty or too long answer, a timeout and a network error
/// return the template text with a reason. Cancelling the caller's token cancels the request and is not replaced by the template.
/// Neither the key nor the full prompt is logged.
/// </summary>
public sealed class LlmExplanationGenerator(TemplateExplanationGenerator template, ExplanationOptions options, IChatClient? chat = null)
    : IExplanationGenerator
{
    public async Task<ExplanationResult> ExplainAsync(EvaluationRecord record, ExplanationAudience audience, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (options.Provider == ExplanationConfiguration.Template)
        {
            return await template.ExplainAsync(record, audience, ct);
        }

        if (chat is null)
        {
            return await Fallback(record, audience, ExplanationConfiguration.Unavailable(options), ct);
        }

        var model = ExplanationConfiguration.Model(options);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            var response = await chat.GetResponseAsync(
                ExplanationPrompt.Create(record, audience),
                new ChatOptions { ModelId = model, ToolMode = ChatToolMode.None },
                linked.Token);
            var text = response.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return await Fallback(record, audience, "The model returned an empty explanation.", ct);
            }

            if (text.Length > options.MaxOutputCharacters)
            {
                return await Fallback(record, audience, $"The model response exceeded {options.MaxOutputCharacters} characters.", ct);
            }

            return new ExplanationResult(text, options.Provider, model, ExplanationPrompt.Version, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return await Fallback(record, audience, $"The model did not respond within {options.TimeoutSeconds} seconds.", ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return await Fallback(record, audience, $"{options.Provider} request failed.", ct);
        }
    }

    private async Task<ExplanationResult> Fallback(EvaluationRecord record, ExplanationAudience audience, string reason, CancellationToken ct)
    {
        var templateResult = await template.ExplainAsync(record, audience, ct);
        return templateResult with { FallbackReason = reason };
    }
}
