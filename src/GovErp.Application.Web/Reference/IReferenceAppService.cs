using GovErp.Application.Web.Common;
using GovErp.Application.Web.Reference.Contracts;

namespace GovErp.Application.Web.Reference;

public interface IReferenceAppService
{
    Task<SegmentsVm> GetSegmentsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<CombinationVm>> GetCombinationsAsync(ActorContext actor, CancellationToken ct = default);
    Task<RuleSetVm> GetRulesAsync(ActorContext actor, CancellationToken ct = default);

    /// <summary>Описание правила, действующая версия и история версий. Неизвестный код — NotFoundException.</summary>
    Task<RuleDetailVm> GetRuleDetailAsync(string ruleId, ActorContext actor, CancellationToken ct = default);

    /// <summary>Пересказ правила для аудитории (LLM или написанное описание при откате). Решений не меняет и не сохраняется.</summary>
    Task<Explanation.ExplanationResult> ExplainRuleAsync(string ruleId, Explanation.ExplanationAudience audience, ActorContext actor,
        CancellationToken ct = default);
    Task<IReadOnlyList<VendorVm>> GetVendorsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderVm>> GetPurchaseOrdersAsync(ActorContext actor, CancellationToken ct = default);
    Task<FundDetailVm> GetFundAsync(string code, ActorContext actor, CancellationToken ct = default);
    Task<GrantDetailVm> GetGrantAsync(string code, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<UserVm>> GetUsersAsync(ActorContext actor, CancellationToken ct = default);
    Task<RoleMatrixVm> GetRoleMatrixAsync(ActorContext actor, CancellationToken ct = default);
}
