using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Reference.Commands;
using GovErp.Application.Web.Reference.Contracts;

namespace GovErp.Application.Web.Reference;

/// <summary>Validation rules: their versions, their written descriptions and new versions.</summary>
public interface IRuleAppService
{
    /// <summary>Every version of every rule; CurrentFingerprint is that of the general set on the business date.</summary>
    Task<RuleSetVm> GetRulesAsync(ActorContext actor, CancellationToken ct = default);

    /// <summary>Rule description, current version and version history. An unknown code throws NotFoundException.</summary>
    Task<RuleDetailVm> GetRuleDetailAsync(string ruleId, ActorContext actor, CancellationToken ct = default);

    /// <summary>Retells the rule for an audience (LLM, or the written description on fallback). Changes no decisions and is not stored.</summary>
    Task<ExplanationResult> ExplainRuleAsync(string ruleId, ExplanationAudience audience, ActorContext actor, CancellationToken ct = default);

    Task<CommandResult<RuleVm>> CreateVersionAsync(NewRuleVersionCommand cmd, ActorContext actor, CancellationToken ct = default);
}
