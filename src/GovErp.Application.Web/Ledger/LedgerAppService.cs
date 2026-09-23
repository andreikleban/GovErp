using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Ledger.Contracts;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Ledger;

/// <summary>Журнал и периоды на чтение; SourceRef проводки совпадает с Reference инвойса, который её вызвал (см. PostingAppService).</summary>
public sealed class LedgerAppService(ITenantOperationRunner runner) : ILedgerAppService
{
    public Task<IReadOnlyList<JournalEntryVm>> ListJournalAsync(JournalFilter filter, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<JournalEntryVm>>(actor, async (sp, token) =>
        {
            var invoices = sp.GetRequiredService<IVendorInvoiceRepository>();
            var source = string.IsNullOrWhiteSpace(filter.Source) ? null : filter.Source.Trim();
            var fund = string.IsNullOrWhiteSpace(filter.Fund) ? null : filter.Fund.Trim();
            var entries = (await sp.GetRequiredService<IJournalRepository>().ListAsync(token))
                .Where(e => source is null || e.SourceRef.Contains(source, StringComparison.OrdinalIgnoreCase))
                .Where(e => filter.PeriodYear is null || e.PeriodYear == filter.PeriodYear)
                .Where(e => filter.PeriodMonth is null || e.PeriodMonth == filter.PeriodMonth)
                .Where(e => fund is null || e.Lines.Any(l => l.Account.Fund.Value == fund))
                .OrderByDescending(e => e.PostedAt);
            var result = new List<JournalEntryVm>();
            foreach (var e in entries)
            {
                var invoice = await invoices.FindByReferenceAsync(e.SourceRef, token);
                result.Add(LedgerMapping.ToVm(e, invoice?.Id, invoice?.Reference));
            }

            return result;
        }, ct);

    public Task<IReadOnlyList<FiscalPeriodVm>> ListPeriodsAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<FiscalPeriodVm>>(actor, async (sp, token) =>
            (await sp.GetRequiredService<IFiscalPeriodRepository>().ListAsync(token))
                .OrderBy(p => p.Year).ThenBy(p => p.Month)
                .Select(p => new FiscalPeriodVm(p.Year, p.Month, p.Status.ToString())).ToList(), ct);
}
