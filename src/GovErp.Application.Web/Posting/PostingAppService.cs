using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Validation.Contracts;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

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
                return CommandResult<InvoiceVm>.Forbidden("Only the budget officer or finance director posts.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.Status != InvoiceStatus.Approved)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"Invoice is {invoice.Status}.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var (record, restarted) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Post, actor, token);
            if (restarted)
            {
                // The new evaluation and the new cycle are saved: approvers will see the invoice again (REVALIDATION_REQUIRED).
                ws.Audit.Record(actor, "ApprovalCycleRestarted", invoice.Reference, record.Id.ToString(), new { Reason = "rule set or route changed since approval" });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token),
                    "REVALIDATION_REQUIRED: rules or the approval route changed since approval; the invoice returned to approval.");
            }

            if (record.PostingCheck is not { Passed: true })
            {
                ws.Audit.Record(actor, "PostRefused", invoice.Reference, record.Id.ToString(), new { Failures = record.PostingCheck?.Failures });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), string.Join(" ", record.PostingCheck?.Failures ?? []));
            }

            var cv = invoice.ContentVersion;
            var fy = FiscalYear.FromDate(invoice.PostingDate);
            var charged = Money.Zero;
            foreach (var id in invoice.ReservationRefs)
            {
                var line = await ws.BudgetLines.FindByReservationAsync(id, token) ?? throw new LedgerException($"Reservation {id} not found.");
                charged += line.Reservations.Single(r => r.Id == id).Amount;
                line.Commit(id, invoice.Id, cv);
            }

            foreach (var id in invoice.EncumbranceClaimRefs)
            {
                var encumbrance = await ws.Encumbrances.FindByClaimAsync(id, token) ?? throw new LedgerException($"Claim {id} not found.");
                var amount = encumbrance.Claims.Single(c => c.Id == id).Amount;
                encumbrance.ConsumeClaim(id, invoice.Id, cv);
                var line = await ws.BudgetLines.FindAsync(encumbrance.Account, fy, token)
                    ?? throw new LedgerException($"Budget line {encumbrance.Account} {fy} not found.");
                line.RecordLiquidation(amount);
                charged += amount;
            }

            foreach (var id in invoice.PoBillingClaimRefs)
            {
                (await ws.Encumbrances.FindByClaimAsync(id, token) ?? throw new LedgerException($"Billing claim {id} not found."))
                    .ConsumeBillingClaim(id, invoice.Id, cv);
            }

            // Consistency invariant §4: actuals grow by exactly the invoice amount. A violation is a program error, not a refusal.
            if (charged != invoice.Total)
            {
                throw new InvalidOperationException($"Posting would change actuals by {charged}, expected {invoice.Total}.");
            }

            var (py, pm) = FiscalPeriod.KeyFor(invoice.PostingDate);
            var period = await ws.Periods.FindAsync(py, pm, token) ?? throw new NotFoundException($"Fiscal period {py}-{pm:00} not found.");
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
            return (await ws.Journal.ListBySourceAsync(invoice.Reference, token))
                .SelectMany(e => e.Lines)
                .Select(l => new PreviewLineVm(l.Account.ToString(), l.Family.ToString(), l.Debit.Amount, l.Credit.Amount, l.Description))
                .ToList();
        }, ct);
}
