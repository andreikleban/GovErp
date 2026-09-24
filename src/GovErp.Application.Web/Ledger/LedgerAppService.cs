using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Ledger.Contracts;
using GovErp.Application.Web.Posting;
using GovErp.Domain.ChartOfAccounts.Repositories;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Ledger;

/// <summary>
/// Read-only journal, fund balances and periods. An expenditure entry's SourceRef is the invoice reference; a payment entry appends /payment.
/// </summary>
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
                var invoice = await invoices.FindByReferenceAsync(PaymentSource.InvoiceReference(e.SourceRef), token);
                result.Add(LedgerMapping.ToVm(e, invoice?.Id, invoice?.Reference));
            }

            return result;
        }, ct);

    public Task<IReadOnlyList<FiscalPeriodVm>> ListPeriodsAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<FiscalPeriodVm>>(actor, async (sp, token) =>
            (await sp.GetRequiredService<IFiscalPeriodRepository>().ListAsync(token))
                .OrderBy(p => p.Year).ThenBy(p => p.Month)
                .Select(p => new FiscalPeriodVm(p.Year, p.Month, p.Status.ToString())).ToList(), ct);

    public Task<IReadOnlyList<FundReportVm>> ListFundReportsAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<FundReportVm>>(actor, async (sp, token) =>
        {
            var year = FiscalYear.FromDate(sp.GetRequiredService<IClock>().BusinessDate);
            var funds = await sp.GetRequiredService<IFundRepository>().ListAsync(token);
            var lines = await sp.GetRequiredService<IBudgetLineRepository>().ListAsync(year, token);
            var journal = await sp.GetRequiredService<IJournalRepository>().ListAsync(token);
            return funds.OrderBy(f => f.Code.Value, StringComparer.Ordinal).Select(fund =>
            {
                var own = lines.Where(l => l.Account.Fund == fund.Code).ToList();
                var financial = journal.SelectMany(e => e.Lines)
                    .Where(l => l.Account.Fund == fund.Code && l.Family == LedgerFamily.Financial).ToList();
                var debit = financial.Aggregate(Money.Zero, (sum, line) => sum + line.Debit);
                var credit = financial.Aggregate(Money.Zero, (sum, line) => sum + line.Credit);
                return new FundReportVm(fund.Code.Value, fund.Name, fund.Type.ToString(), fund.Basis.ToString(), year.Year,
                    own.Aggregate(Money.Zero, (sum, line) => sum + line.Amended).Amount,
                    own.Aggregate(Money.Zero, (sum, line) => sum + line.Actuals).Amount,
                    own.Aggregate(Money.Zero, (sum, line) => sum + line.Encumbered).Amount,
                    own.Aggregate(Money.Zero, (sum, line) => sum + line.Held).Amount,
                    own.Aggregate(Money.Zero, (sum, line) => sum + line.Available).Amount,
                    debit.Amount, credit.Amount);
            }).ToList();
        }, ct);
}
