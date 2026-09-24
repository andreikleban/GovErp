using GovErp.Application.Web.Reference.Contracts;

namespace GovErp.Application.Web.Explanation;

/// <summary>
/// Retells a rule for an audience. The facts come from the written description and the current rule version;
/// when the model is unavailable, the description itself is returned with the fallback reason.
/// </summary>
public interface IRuleExplanationGenerator
{
    Task<ExplanationResult> ExplainAsync(RuleDescriptionVm description, RuleVm? current, ExplanationAudience audience, CancellationToken ct = default);
}
