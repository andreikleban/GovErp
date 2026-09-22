using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Budget.Commands;
using GovErp.Application.Web.Budget.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Domain.Ledger.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Budget;

public sealed class BudgetAppService(ITenantOperationRunner runner) : IBudgetAppService
{
    public Task<IReadOnlyList<BudgetLineVm>> ListAsync(int fiscalYear, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<BudgetLineVm>>(actor, async (sp, token) =>
        {
            var fy = new FiscalYear(fiscalYear);
            var lines = await sp.GetRequiredService<IBudgetLineRepository>().ListAsync(fy, token);
            var openings = await sp.GetRequiredService<IOpeningBalanceRepository>().ListAsync(fy, token);
            return lines.OrderBy(l => l.Account.ToString(), StringComparer.Ordinal).Select(l => BudgetMapping.ToVm(l, openings)).ToList();
        }, ct);

    public Task<CommandResult<BudgetLineVm>> AmendAsync(AmendBudgetCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "AmendBudget", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Overriders))
            {
                return CommandResult<BudgetLineVm>.Forbidden("Only the budget officer or finance director amends budgets.");
            }

            var lines = sp.GetRequiredService<IBudgetLineRepository>();
            var line = await lines.FindAsync(AccountCode.Parse(cmd.Account), new FiscalYear(cmd.FiscalYear), token)
                ?? throw new NotFoundException($"Budget line {cmd.Account} FY{cmd.FiscalYear} not found.");
            line.Amend(Money.Of(cmd.Amount), cmd.Reference, cmd.EffectiveDate);   // дата вне FY → LedgerException → Refused
            sp.GetRequiredService<IAuditTrail>().Record(actor, "BudgetAmended", cmd.Account, cmd.Envelope.CommandId.ToString(),
                new { cmd.FiscalYear, cmd.Amount, cmd.Reference, cmd.EffectiveDate, Amended = line.Amended.Amount });
            return CommandResult<BudgetLineVm>.Accepted(await BudgetMapping.ToVmAsync(line, sp.GetRequiredService<IOpeningBalanceRepository>(), token));
        }, ct: ct);
}
