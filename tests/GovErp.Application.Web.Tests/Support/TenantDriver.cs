using GovErp.Application.Web.Approvals;
using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Budget.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Posting;
using GovErp.Infrastructure.Persistence;
using GovErp.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Tests;

/// <summary>Акторы и сценарии одного тенанта. Каждый вызов — отдельная операция runner'а (свой scope и DbContext).</summary>
public sealed class TenantDriver(SqlServerFixture fixture, TenantId tenant)
{
    public TenantId Tenant { get; } = tenant;
    public ActorContext Clerk => Actor(SpringfieldData.ClerkId, "ap.clerk", Roles.ApClerk);
    public ActorContext FireChief => Actor(new UserId(MasterSeed.FireChiefId), "fire.chief", Roles.DepartmentHead, "6000");
    public ActorContext PoliceChief => Actor(new UserId(MasterSeed.PoliceChiefId), "police.chief", Roles.DepartmentHead, "3000");
    public ActorContext PwDirector => Actor(new UserId(MasterSeed.PwDirectorId), "pw.director", Roles.DepartmentHead, "4000");
    public ActorContext WaterDirector => Actor(new UserId(MasterSeed.WaterDirectorId), "water.director", Roles.DepartmentHead, "5000");
    public ActorContext GrantsManager => Actor(new UserId(MasterSeed.GrantsManagerId), "grants.manager", Roles.GrantsManager);
    public ActorContext BudgetOfficer => Actor(new UserId(MasterSeed.BudgetOfficerId), "budget.officer", Roles.BudgetOfficer);
    public ActorContext FinanceDirector => Actor(new UserId(MasterSeed.FinanceDirectorId), "finance.director", Roles.FinanceDirector);
    public ActorContext[] Approvers => [FireChief, PoliceChief, PwDirector, WaterDirector, GrantsManager, BudgetOfficer, FinanceDirector];

    public TestHooks Hooks => fixture.Hooks;
    public T Service<T>() where T : notnull => fixture.App.GetRequiredService<T>();
    public static CommandEnvelope Env(string? rowVersion = null) => new(Guid.NewGuid(), rowVersion);

    private ActorContext Actor(UserId id, string name, string role, string? dept = null) => new(Tenant, id, name, new HashSet<string> { role }, dept);

    public Task<InvoiceVm> GetAsync(Guid id) => Service<IInvoiceAppService>().GetAsync(id, Clerk);

    public async Task<InvoiceVm> CreateAsync(decimal total, string? po, params (string Account, decimal Amount, int? PoLine)[] lines)
    {
        var r = await Service<IInvoiceAppService>().CreateDraftAsync(new CreateInvoiceCommand(Env(), $"T-{Guid.NewGuid():N}"[..12],
            SpringfieldData.AcmeId, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, new DateOnly(2026, 7, 15), total, po,
            lines.Select(l => new DistributionCommand(l.Account, l.Amount, l.PoLine)).ToList()), Clerk);
        r.IsAccepted.Should().BeTrue(r.Reason);
        return r.Value!;
    }

    public async Task<CommandResult<InvoiceVm>> SubmitAsync(Guid id) =>
        await Service<IInvoiceAppService>().SubmitAsync(new InvoiceActionCommand(Env((await GetAsync(id)).RowVersion), id), Clerk);

    /// <summary>Override всех открытых Soft Stop директором, затем согласование каждого шага подходящим актором — до статуса Approved.</summary>
    public async Task ApproveThroughAsync(Guid id)
    {
        for (var guard = 0; guard < 20; guard++)
        {
            var inv = await GetAsync(id);
            if (inv.Status == "Approved") { return; }
            var eval = inv.LastEvaluation!;
            var soft = eval.Outcomes.FirstOrDefault(o => o.Severity == "SoftStop" && o.OverriddenBy is null);
            if (soft is not null)
            {
                var o = await Service<IApprovalAppService>().OverrideAsync(new OverrideCommand(Env(inv.RowVersion), id, eval.Id, soft.RuleId, soft.Line, "test justification"), FinanceDirector);
                o.IsAccepted.Should().BeTrue(o.Reason);
                continue;
            }

            var step = eval.ApprovalRoute.First(r => !r.IsSatisfied);
            var actor = Approvers.First(a => a.IsInRole(step.Role) && (step.Department is null || a.DepartmentCode == step.Department));
            var r = await Service<IApprovalAppService>().ApproveAsync(new InvoiceActionCommand(Env(inv.RowVersion), id), actor);
            r.IsAccepted.Should().BeTrue(r.Reason);
        }

        throw new InvalidOperationException("Approval loop did not converge.");
    }

    public async Task<CommandResult<InvoiceVm>> PostAsync(Guid id, CommandEnvelope? env = null) =>
        await Service<IPostingAppService>().PostAsync(new InvoiceActionCommand(env ?? Env((await GetAsync(id)).RowVersion), id), FinanceDirector);

    public async Task<BudgetLineVm> BudgetAsync(string account) =>
        (await Service<IBudgetAppService>().ListAsync(2026, BudgetOfficer)).Single(b => b.Account == account);

    public Task<T> WithDbAsync<T>(Func<GovErpDbContext, Task<T>> query) =>
        Service<ITenantOperationRunner>().QueryAsync(Clerk, (sp, _) => query(sp.GetRequiredService<GovErpDbContext>()));
}
