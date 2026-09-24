using System.Text;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Reference.Contracts;

namespace GovErp.Infrastructure.Explanation;

/// <summary>A deterministic retelling of the written description: the Template mode and the fallback when the model is unavailable.</summary>
public static class TemplateRuleExplanation
{
    public const string Provider = "Template";
    public const string Version = "rule-template-1";

    public static ExplanationResult Create(RuleDescriptionVm d, RuleVm? current, ExplanationAudience audience, string? fallbackReason = null)
    {
        var text = new StringBuilder();
        text.AppendLine($"{d.Title} ({d.RuleId}), step {d.Step}: {d.StepName}.");
        text.AppendLine(d.Checks);
        if (audience != ExplanationAudience.Public && d.Parameters.Count > 0)
        {
            text.AppendLine("Parameters: " + string.Join("; ", d.Parameters.Select(p =>
                $"{p.Key} = {current?.Parameters.GetValueOrDefault(p.Key) ?? "n/a"} ({p.Value})")) + ".");
        }

        text.AppendLine($"When it fires: {d.WhenFired}");
        text.AppendLine($"What to do: {d.Resolution}");
        if (audience == ExplanationAudience.Auditor)
        {
            if (current is not null)
            {
                text.AppendLine($"Applied version {current.Version} ({current.Layer} layer), effective {current.EffectiveFrom:yyyy-MM-dd}.");
            }

            text.AppendLine($"Facts evaluated: {string.Join("; ", d.Facts)}.");
            text.AppendLine($"Source: {d.Source}");
            text.AppendLine($"To confirm with a government finance expert: {string.Join(" ", d.SmeQuestions)}");
        }

        return new ExplanationResult(text.ToString().TrimEnd(), Provider, null, Version, fallbackReason);
    }
}
