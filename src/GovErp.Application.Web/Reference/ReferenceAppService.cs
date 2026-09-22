using System.Globalization;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Reference.Contracts;
using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Repositories;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Reference;

/// <summary>Справочники только на чтение: сегменты, комбинации, правила, поставщики и PO с балансами encumbrance.</summary>
public sealed class ReferenceAppService(ITenantOperationRunner runner) : IReferenceAppService
{
    private static readonly IReadOnlyDictionary<string, string> NoAttributes = new Dictionary<string, string>();

    public Task<SegmentsVm> GetSegmentsAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var reference = sp.GetRequiredService<IReferenceDataRepository>();
            var funds = (await sp.GetRequiredService<IFundRepository>().ListAsync(token))
                .OrderBy(f => f.Code.Value, StringComparer.Ordinal)
                .Select(f => new SegmentValueVm(f.Code.Value, f.Name, f.IsActive, new Dictionary<string, string>
                {
                    ["Type"] = f.Type.ToString(),
                    ["Basis"] = f.Basis.ToString(),
                    ["ControlMode"] = f.ControlMode.ToString(),
                    ["GrantPolicy"] = f.GrantPolicy.ToString(),
                })).ToList();
            var departments = (await reference.ListDepartmentsAsync(token))
                .OrderBy(d => d.Code.Value, StringComparer.Ordinal)
                .Select(d => new SegmentValueVm(d.Code.Value, d.Name, d.IsActive, NoAttributes)).ToList();
            var objects = (await reference.ListObjectsAsync(token))
                .OrderBy(o => o.Code.Value, StringComparer.Ordinal)
                .Select(o => new SegmentValueVm(o.Code.Value, o.Name, o.IsActive,
                    new Dictionary<string, string> { ["Category"] = o.Category.ToString() })).ToList();
            var grants = (await sp.GetRequiredService<IGrantRepository>().ListAsync(token))
                .OrderBy(g => g.Code.Value, StringComparer.Ordinal)
                .Select(g => new SegmentValueVm(g.Code.Value, g.Name, g.Status == GrantStatus.Active, new Dictionary<string, string>
                {
                    ["Sponsor"] = g.Sponsor,
                    ["IsFederal"] = g.IsFederal.ToString(),
                    ["PeriodFrom"] = Date(g.Period.From),
                    ["PeriodTo"] = g.Period.To is { } to ? Date(to) : "",
                    ["Status"] = g.Status.ToString(),
                })).ToList();
            return new SegmentsVm(funds, departments, objects, grants);
        }, ct);

    public Task<IReadOnlyList<CombinationVm>> GetCombinationsAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<CombinationVm>>(actor, async (sp, token) =>
            (await sp.GetRequiredService<IAccountCombinationRepository>().ListAsync(token))
                .OrderBy(c => c.Code.ToString(), StringComparer.Ordinal)
                .Select(c => new CombinationVm(c.Code.ToString(), c.Status.ToString(), c.EffectiveFrom, c.EffectiveTo, c.Source.ToString()))
                .ToList(), ct);

    /// <summary>CurrentFingerprint — отпечаток общего набора без scope на бизнес-дату; scoped-правила показаны с ScopeFund и ScopeGrant.</summary>
    public Task<RuleSetVm> GetRulesAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var all = await sp.GetRequiredService<IRuleDefinitionRepository>().ListAsync(token);
            var clock = sp.GetRequiredService<IClock>();
            var rules = all
                .OrderBy(r => r.Step).ThenBy(r => r.RuleId, StringComparer.Ordinal).ThenBy(r => r.Layer).ThenBy(r => r.Version)
                .ThenBy(r => r.ScopeFund, StringComparer.Ordinal).ThenBy(r => r.ScopeGrant, StringComparer.Ordinal)
                .Select(r => new RuleVm(r.RuleId, r.Version, (int)r.Step, r.Layer.ToString(), r.ScopeFund, r.ScopeGrant, r.Severity?.ToString(),
                    r.Parameters, r.OverridableBy.Select(role => role.ToString()).ToList(), r.EffectiveFrom, r.EffectiveTo, r.IsEnabled, r.Message))
                .ToList();
            return new RuleSetVm(rules, RuleResolution.Resolve(all, clock.BusinessDate).Fingerprint, RuleResolution.EngineVersion);
        }, ct);

    public Task<IReadOnlyList<VendorVm>> GetVendorsAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<VendorVm>>(actor, async (sp, token) =>
            (await sp.GetRequiredService<IVendorRepository>().ListAsync(token))
                .OrderBy(v => v.Code, StringComparer.Ordinal)
                .Select(v => new VendorVm(v.Id, v.Code, v.Name, v.Status.ToString(), v.SamRegistered))
                .ToList(), ct);

    /// <summary>Строки PO дополнены балансами Encumbrance той же строки; без encumbrance — авторизовано по строке, остаток 0.</summary>
    public Task<IReadOnlyList<PurchaseOrderVm>> GetPurchaseOrdersAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<PurchaseOrderVm>>(actor, async (sp, token) =>
        {
            var encumbrances = sp.GetRequiredService<IEncumbranceRepository>();
            var result = new List<PurchaseOrderVm>();
            foreach (var po in (await sp.GetRequiredService<IPurchaseOrderRepository>().ListAsync(token)).OrderBy(p => p.Number, StringComparer.Ordinal))
            {
                var lines = new List<PurchaseOrderLineVm>();
                foreach (var line in po.Lines.OrderBy(l => l.LineNo))
                {
                    var e = await encumbrances.FindByPoLineAsync(po.LineRef(line.LineNo), token);
                    lines.Add(new PurchaseOrderLineVm(line.LineNo, line.Account.ToString(), line.Amount.Amount,
                        e?.AuthorizedPoAmount.Amount ?? line.Amount.Amount, e?.AlreadyPostedAgainstPo.Amount ?? 0m, e?.Remaining.Amount ?? 0m));
                }

                result.Add(new PurchaseOrderVm(po.Number, po.VendorId, po.Status.ToString(), lines));
            }

            return result;
        }, ct);

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
