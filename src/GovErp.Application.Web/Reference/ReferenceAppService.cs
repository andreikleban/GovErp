using System.Globalization;
using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Budget.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Reference.Contracts;
using GovErp.Application.Web.Tenancy;
using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Repositories;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Reference;

/// <summary>
/// Read-only reference data: segments, combinations, funds and grants, vendors, POs with encumbrance balances, users and roles.
/// </summary>
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

    public Task<IReadOnlyList<VendorVm>> GetVendorsAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<VendorVm>>(actor, async (sp, token) =>
            (await sp.GetRequiredService<IVendorRepository>().ListAsync(token))
                .OrderBy(v => v.Code, StringComparer.Ordinal)
                .Select(v => new VendorVm(v.Id, v.Code, v.Name, v.Status.ToString(), v.SamRegistered))
                .ToList(), ct);

    /// <summary>
    /// PO lines are enriched with the Encumbrance balances of the same line; without an encumbrance: authorized per line, remaining 0.
    /// </summary>
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

    /// <summary>
    /// Fund card: attributes, combinations by Code.Fund and budget lines of all tracked accounts of this fund.
    /// </summary>
    public Task<FundDetailVm> GetFundAsync(string code, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var fundCode = new FundCode(code);
            var fund = await sp.GetRequiredService<IFundRepository>().FindAsync(fundCode, token)
                ?? throw new NotFoundException(AppErrors.FundNotFound, ("fund", code));
            var combinations = await CombinationsAsync(sp, c => c.Code.Fund == fundCode, token);
            var lines = await BudgetLinesAsync(sp, l => l.Account.Fund == fundCode, token);
            return new FundDetailVm(fund.Code.Value, fund.Name, fund.Type.ToString(), fund.Basis.ToString(), fund.ControlMode.ToString(),
                fund.GrantPolicy.ToString(), fund.AllowedDepartments.Select(d => d.Value).ToList(), fund.AllowedObjects.Select(o => o.Value).ToList(),
                fund.IsActive, combinations, lines);
        }, ct);

    /// <summary>
    /// Grant card: attributes, combinations by Code.Grant and budget lines of all tracked accounts of this grant.
    /// </summary>
    public Task<GrantDetailVm> GetGrantAsync(string code, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var grantCode = new GrantCode(code);
            var grant = await sp.GetRequiredService<IGrantRepository>().FindAsync(grantCode, token)
                ?? throw new NotFoundException(AppErrors.GrantNotFound, ("grant", code));
            var combinations = await CombinationsAsync(sp, c => c.Code.Grant == grantCode, token);
            var lines = await BudgetLinesAsync(sp, l => l.Account.Grant == grantCode, token);
            return new GrantDetailVm(grant.Code.Value, grant.Name, grant.Sponsor, grant.IsFederal, grant.Period.From, grant.Period.To,
                grant.AllowedDepartments.Select(d => d.Value).ToList(), grant.AllowableObjects.Select(o => o.Value).ToList(),
                grant.Status.ToString(), combinations, lines);
        }, ct);

    public Task<IReadOnlyList<UserVm>> GetUsersAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<UserVm>>(actor, async (sp, token) =>
            (await sp.GetRequiredService<ITenantUserDirectory>().ListAsync(actor.TenantId, token))
                .OrderBy(u => u.UserName, StringComparer.Ordinal)
                .Select(u => new UserVm(u.Id, u.UserName, u.DisplayName, u.Roles, u.DepartmentCode))
                .ToList(), ct);

    /// <summary>
    /// Which role may do what, from the rules in force on the business date (see RoleMatrix).
    /// </summary>
    public Task<RoleMatrixVm> GetRoleMatrixAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var rules = await sp.GetRequiredService<IRuleDefinitionRepository>().ListAsync(token);
            return RoleMatrix.From(RuleResolver.Default.Resolve(rules, sp.GetRequiredService<IClock>().BusinessDate));
        }, ct);

    private static async Task<IReadOnlyList<CombinationVm>> CombinationsAsync(IServiceProvider sp, Func<AccountCombination, bool> matches, CancellationToken ct) =>
        (await sp.GetRequiredService<IAccountCombinationRepository>().ListAsync(ct))
            .Where(matches)
            .OrderBy(c => c.Code.ToString(), StringComparer.Ordinal)
            .Select(c => new CombinationVm(c.Code.ToString(), c.Status.ToString(), c.EffectiveFrom, c.EffectiveTo, c.Source.ToString()))
            .ToList();

    /// <summary>
    /// Budget lines for all years that have fiscal periods: a BudgetLine has a year, a PO/fund/grant does not.
    /// </summary>
    private static async Task<IReadOnlyList<BudgetLineVm>> BudgetLinesAsync(IServiceProvider sp, Func<BudgetLine, bool> matches, CancellationToken ct)
    {
        var budgetLines = sp.GetRequiredService<IBudgetLineRepository>();
        var openingBalances = sp.GetRequiredService<IOpeningBalanceRepository>();
        var years = (await sp.GetRequiredService<IFiscalPeriodRepository>().ListAsync(ct)).Select(p => p.Year).Distinct();
        var result = new List<BudgetLineVm>();
        foreach (var year in years)
        {
            var fy = new FiscalYear(year);
            var openings = await openingBalances.ListAsync(fy, ct);
            result.AddRange((await budgetLines.ListAsync(fy, ct)).Where(matches).Select(l => BudgetMapping.ToVm(l, openings)));
        }

        return result.OrderBy(l => l.FiscalYear).ThenBy(l => l.Account, StringComparer.Ordinal).ToList();
    }

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
