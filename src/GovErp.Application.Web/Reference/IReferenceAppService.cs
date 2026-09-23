using GovErp.Application.Web.Common;
using GovErp.Application.Web.Reference.Contracts;

namespace GovErp.Application.Web.Reference;

public interface IReferenceAppService
{
    Task<SegmentsVm> GetSegmentsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<CombinationVm>> GetCombinationsAsync(ActorContext actor, CancellationToken ct = default);
    Task<RuleSetVm> GetRulesAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<VendorVm>> GetVendorsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderVm>> GetPurchaseOrdersAsync(ActorContext actor, CancellationToken ct = default);
    Task<FundDetailVm> GetFundAsync(string code, ActorContext actor, CancellationToken ct = default);
    Task<GrantDetailVm> GetGrantAsync(string code, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<UserVm>> GetUsersAsync(ActorContext actor, CancellationToken ct = default);
    Task<RoleMatrixVm> GetRoleMatrixAsync(ActorContext actor, CancellationToken ct = default);
}
