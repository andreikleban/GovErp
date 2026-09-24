using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Validation;
using GovErp.Application.Web.Validation.Contracts;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;
using GovErp.Domain.Validation.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GovErp.Application.Web.Posting;

/// <summary>
/// Any failure after the first aggregate change is thrown as an exception (period, journal balance, a foreign claim):
/// the runner does not call SaveChanges and the transaction rolls back, so neither the budget, nor claims, nor the status change.
/// A repeated Post is impossible: the status is already Posted; a concurrent second Post fails on the unique JournalEntries.SourceRef → Conflict.
/// </summary>
public sealed class PostingAppService(ITenantOperationRunner runner) : IPostingAppService
{
    /// <summary>
    /// Spec §5.4: a Serializable transaction re-validates; if the rules or the route changed, the invoice goes into a new approval
    /// cycle (Reevaluate) and Post is refused; otherwise it consumes its reservations and claims, liquidates, journals and sets the status.
    /// </summary>
    public Task<CommandResult<InvoiceVm>> PostAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "PostInvoice", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Posters))
            {
                return CommandResult<InvoiceVm>.Forbidden(AppErrors.OnlyPostersPost);
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.Status != InvoiceStatus.Approved)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), AppErrors.InvoiceStatus, ("status", invoice.Status));
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var (record, restarted) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Post, actor, token);
            if (restarted)
            {
                // The new evaluation and the new cycle are saved: approvers will see the invoice again (REVALIDATION_REQUIRED).
                ws.Audit.Record(actor, "ApprovalCycleRestarted", invoice.Reference, record.Id.ToString(), new { Reason = "rule set or route changed since approval" });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token),
                    AppErrors.RevalidationRequired);
            }

            if (record.PostingCheck is not { Passed: true })
            {
                ws.Audit.Record(actor, "PostRefused", invoice.Reference, record.Id.ToString(), new { Failures = record.PostingCheck?.Failures.Select(f => f.Code) });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), PostingRefusal(record.PostingCheck));
            }

            var cv = invoice.ContentVersion;
            var fy = FiscalYear.FromDate(invoice.PostingDate);
            var charged = Money.Zero;
            foreach (var id in invoice.ReservationRefs)
            {
                var line = await ws.BudgetLines.FindByReservationAsync(id, token) ?? throw new LedgerException(LedgerErrors.UnknownReservation);
                charged += line.Reservations.Single(r => r.Id == id).Amount;
                line.Commit(id, invoice.Id, cv);
            }

            foreach (var id in invoice.EncumbranceClaimRefs)
            {
                var encumbrance = await ws.Encumbrances.FindByClaimAsync(id, token) ?? throw new LedgerException(LedgerErrors.UnknownClaim);
                var amount = encumbrance.Claims.Single(c => c.Id == id).Amount;
                encumbrance.ConsumeClaim(id, invoice.Id, cv);
                var line = await ws.BudgetLines.FindAsync(encumbrance.Account, fy, token)
                    ?? throw new LedgerException(LedgerErrors.BudgetLineNotFound, ("account", encumbrance.Account), ("fiscalYear", fy.Year));
                line.RecordLiquidation(amount);
                charged += amount;
            }

            foreach (var id in invoice.PoBillingClaimRefs)
            {
                (await ws.Encumbrances.FindByClaimAsync(id, token) ?? throw new LedgerException(LedgerErrors.UnknownBillingClaim))
                    .ConsumeBillingClaim(id, invoice.Id, cv);
            }

            // Consistency invariant §4: actuals grow by exactly the invoice amount. A violation is a program error, not a refusal.
            if (charged != invoice.Total)
            {
                throw new InvalidOperationException(Problem.Of(LedgerErrors.PostingTotalMismatch, ("charged", charged), ("expected", invoice.Total)).ToString());
            }

            var (py, pm) = FiscalPeriod.KeyFor(invoice.PostingDate);
            var period = await ws.Periods.FindAsync(py, pm, token) ?? throw new NotFoundException(AppErrors.PeriodNotFound, ("period", $"{py}-{pm:00}"));
            var lines = record.PostingPreview
                .Select(p => new JournalLine(p.Account, Enum.Parse<LedgerFamily>(p.Family), p.Debit, p.Credit, p.Description)).ToList();
            await ws.Journal.AddAsync(JournalEntry.Create(invoice.Reference, lines, period, actor.UserId, ws.Clock.Now), token);
            invoice.Post(record.Id, cv, invoice.ApprovalCycleId!.Value, record.RuleFingerprint, ws.Clock.Now);
            ws.Audit.Record(actor, "InvoicePosted", invoice.Reference, record.Id.ToString(), new { Charged = charged.Amount, Lines = lines.Count });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, System.Data.IsolationLevel.Serializable, ct);

    public Task<IReadOnlyList<PreviewLineVm>> GetJournalAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<PreviewLineVm>>(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(invoiceId, token);
            var posted = await ws.Journal.ListBySourceAsync(invoice.Reference, token);
            var paid = await ws.Journal.ListBySourceAsync(PaymentSource.For(invoice.Reference), token);
            return posted.Concat(paid)
                .SelectMany(e => e.Lines)
                .Select(l => new PreviewLineVm(l.Account.ToString(), l.Family.ToString(), l.Debit.Amount, l.Credit.Amount, l.Description))
                .ToList();
        }, ct);

    /// <summary>
    /// Dr Accounts Payable, Cr Cash, once per fund, on the business date. Cash on hand is not tested; no bank file is sent.
    /// </summary>
    public Task<CommandResult<InvoiceVm>> PayAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "PayInvoice", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Posters))
            {
                return CommandResult<InvoiceVm>.Forbidden(AppErrors.OnlyPostersPay);
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var vendor = await ws.Vendors.FindAsync(invoice.VendorId, token) ?? throw new NotFoundException(AppErrors.VendorNotFound, ("id", invoice.VendorId));
            var active = vendor.Status == VendorStatus.Active;
            var businessDate = ws.Clock.BusinessDate;
            if (invoice.PaidAt is not null)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), PayablesErrors.AlreadyPaid);
            }

            if (!invoice.ReadyForPaymentHandoff(active, businessDate))
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), PayablesErrors.PaymentNotReady,
                    ("status", invoice.Status), ("dueDate", invoice.DueDate));
            }

            var options = sp.GetRequiredService<IOptions<PostingOptions>>().Value;
            var department = new DepartmentCode(options.BalanceSheetDepartment);
            var payable = new ObjectCode(options.AccountsPayableObject);
            var cash = new ObjectCode(options.CashObject);
            var lines = new List<JournalLine>();
            foreach (var fund in invoice.Distributions.GroupBy(d => d.Account.Fund))
            {
                var total = fund.Aggregate(Money.Zero, (sum, line) => sum + line.Amount);
                lines.Add(new JournalLine(new AccountCode(fund.Key, department, payable, null), LedgerFamily.Financial, total, Money.Zero, "Relieve accounts payable"));
                lines.Add(new JournalLine(new AccountCode(fund.Key, department, cash, null), LedgerFamily.Financial, Money.Zero, total, "Cash"));
            }

            var (year, month) = FiscalPeriod.KeyFor(businessDate);
            var period = await ws.Periods.FindAsync(year, month, token) ?? throw new NotFoundException(AppErrors.PeriodNotFound, ("period", $"{year}-{month:00}"));
            await ws.Journal.AddAsync(JournalEntry.Create(PaymentSource.For(invoice.Reference), lines, period, actor.UserId, ws.Clock.Now), token);
            invoice.RecordPayment(active, businessDate, ws.Clock.Now);
            ws.Audit.Record(actor, "InvoicePaid", invoice.Reference, invoice.Id.ToString(), new { invoice.Total.Amount, Lines = lines.Count });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, System.Data.IsolationLevel.Serializable, ct);

    /// <summary>
    /// One reason keeps its own code; several are listed together.
    /// </summary>
    private static Problem PostingRefusal(PostingCheck? check) =>
        check is { Failures.Count: 1 } ? check.Failures[0]
            : Problem.Of(AppErrors.BlockedBy, ("reasons", string.Join(" ", check?.Failures.Select(Messages.Render) ?? [])));
}
