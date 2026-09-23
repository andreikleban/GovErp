using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Budget.Commands;
using GovErp.Application.Web.Budget.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;
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

    public Task<BudgetLineDetailVm> GetLineAsync(string account, int fiscalYear, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var fy = new FiscalYear(fiscalYear);
            var accountCode = AccountCode.Parse(account);
            var line = await sp.GetRequiredService<IBudgetLineRepository>().FindAsync(accountCode, fy, token)
                ?? throw new NotFoundException($"Budget line {account} FY{fiscalYear} not found.");
            var openings = await sp.GetRequiredService<IOpeningBalanceRepository>().ListAsync(fy, token);
            var invoices = sp.GetRequiredService<IVendorInvoiceRepository>();
            var reservations = await BudgetMapping.ReservationsAsync(line, invoices, token);
            var onAccount = (await sp.GetRequiredService<IEncumbranceRepository>().ListAsync(token)).Where(e => e.Account == accountCode);
            var encumbrances = new List<EncumbranceVm>();
            foreach (var e in onAccount.OrderBy(e => e.PoLineRef, StringComparer.Ordinal))
            {
                encumbrances.Add(await BudgetMapping.ToEncumbranceVmAsync(e, invoices, token));
            }

            return new BudgetLineDetailVm(BudgetMapping.ToVm(line, openings), reservations, encumbrances);
        }, ct);

    public Task<IReadOnlyList<AmendmentEntryVm>> ListAmendmentsAsync(int fiscalYear, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<AmendmentEntryVm>>(actor, async (sp, token) =>
        {
            var fy = new FiscalYear(fiscalYear);
            var lines = await sp.GetRequiredService<IBudgetLineRepository>().ListAsync(fy, token);
            return lines.SelectMany(l => l.Amendments.Select(a =>
                    new AmendmentEntryVm(l.Account.ToString(), l.FiscalYear.Year, a.Amount.Amount, a.Reference, a.EffectiveDate)))
                .OrderBy(a => a.EffectiveDate).ThenBy(a => a.Account, StringComparer.Ordinal).ToList();
        }, ct);

    public Task<IReadOnlyList<EncumbranceVm>> ListEncumbrancesAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<EncumbranceVm>>(actor, async (sp, token) =>
        {
            var invoices = sp.GetRequiredService<IVendorInvoiceRepository>();
            var all = await sp.GetRequiredService<IEncumbranceRepository>().ListAsync(token);
            var result = new List<EncumbranceVm>();
            foreach (var e in all.OrderBy(e => e.PoLineRef, StringComparer.Ordinal))
            {
                result.Add(await BudgetMapping.ToEncumbranceVmAsync(e, invoices, token));
            }

            return result;
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
