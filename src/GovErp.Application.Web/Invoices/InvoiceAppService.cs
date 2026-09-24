using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Invoices.Mapping;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Invoices;

/// <summary>
/// Invoice use cases. Rule for all commands: a returned Refused / Forbidden keeps the body's changes together with the receipt
/// (the evaluation record, the refusal audit, a new approval cycle); a refusal after aggregate changes that must not be saved
/// is raised only as a domain exception: the runner turns it into Refused and rolls everything back.
/// </summary>
public sealed class InvoiceAppService(ITenantOperationRunner runner) : IInvoiceAppService
{
    public Task<IReadOnlyList<InvoiceListItemVm>> ListAsync(InvoiceListFilter filter, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<InvoiceListItemVm>>(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var result = new List<InvoiceListItemVm>();
            var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
            var fund = string.IsNullOrWhiteSpace(filter.Fund) ? null : filter.Fund.Trim();
            // Status and fund filters are cheap (document fields) and run before the evaluation is read; "has holds" uses the latest evaluation.
            var matching = (await ws.Invoices.ListAsync(token))
                .Where(i => status is null || string.Equals(i.Status.ToString(), status, StringComparison.OrdinalIgnoreCase))
                .Where(i => fund is null || InvoiceMapping.FundsOf(i).Contains(fund, StringComparer.Ordinal))
                .OrderByDescending(i => i.CreatedAt);
            foreach (var inv in matching)
            {
                var last = await ws.LastEvaluationAsync(inv, token);
                if (filter.HasHolds is { } holds && holds != InvoiceMapping.OpenHolds(last) > 0)
                {
                    continue;
                }

                var vendor = await ws.Vendors.FindAsync(inv.VendorId, token);
                result.Add(InvoiceMapping.ToListItem(inv, vendor?.Name ?? "?", last));
            }

            return result;
        }, ct);

    public Task<InvoiceVm> GetAsync(Guid id, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            return await ws.ToVmAsync(await ws.LoadAsync(id, token), token, withRowVersion: true);
        }, ct);

    public Task<string> SuggestNumberAsync(DateOnly postingDate, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
            IInvoiceNumbering.GeneratedNumber(IInvoiceNumbering.SuggestedPrefix,
                await sp.GetRequiredService<IInvoiceNumbering>().PeekAsync(FiscalYear.FromDate(postingDate), token)), ct);

    public Task<CommandResult<InvoiceVm>> CreateDraftAsync(CreateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "CreateInvoice", cmd, async (sp, token) =>
        {
            if (!actor.IsInRole(Roles.ApClerk))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only AP clerks create invoices.");
            }

            // Everything that can be refused is checked while nothing has changed; the registration number is issued last.
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            if (await NewDraftProblemAsync(ws, cmd, token) is { } problem)
            {
                return CommandResult<InvoiceVm>.Refused(null, problem);
            }

            var invoice = await sp.GetRequiredService<InvoiceRegistration>().RegisterAsync(cmd, actor.UserId, token);
            await ws.Invoices.AddAsync(invoice, token);
            ws.Audit.Record(actor, "InvoiceCreated", invoice.Reference, cmd.Envelope.CommandId.ToString(),
                new { invoice.Number, cmd.Total, Lines = cmd.Distributions.Count, cmd.PoRef });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public async Task<CommandResult<InvoiceVm>> CreateFromPresetAsync(InvoicePreset preset, CommandEnvelope envelope, ActorContext actor,
        CancellationToken ct = default)
    {
        var vendorId = await runner.QueryAsync(actor, async (sp, token) =>
            (await sp.GetRequiredService<IVendorRepository>().ListAsync(token)).Single(v => v.Code == Presets.VendorCode).Id, ct);
        return await CreateDraftAsync(Presets.For(preset, envelope, vendorId), actor, ct);
    }

    public Task<CommandResult<InvoiceVm>> UpdateDraftAsync(UpdateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "UpdateInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.CreatedBy != actor.UserId)
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the author edits a draft.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            if (DemoDateMismatch(cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate) is { } updateDateReason)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), updateDateReason);
            }

            invoice.UpdateHeader(cmd.Number, cmd.VendorId, cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate, cmd.DueDate,
                Money.Of(cmd.Total), cmd.PoRef);   // not Draft → PayablesException → Refused
            invoice.ReplaceDistributions(cmd.Distributions.Select(d => (AccountCode.Parse(d.Account), Money.Of(d.Amount), d.PoLineNo)).ToList());

            if (await ws.Invoices.ExistsDuplicateAsync(invoice.VendorId, invoice.NormalizedInvoiceNumber, invoice.Id, token))
            {
                // The aggregate has already changed: refuse only with an exception so the runner rolls the changes back.
                throw new PayablesException(InvoiceRegistration.DuplicateNumber);
            }

            ws.Audit.Record(actor, "InvoiceUpdated", invoice.Reference, cmd.Envelope.CommandId.ToString(), new { invoice.ContentVersion });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> ValidateAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "ValidateInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            var (record, restarted) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Manual, actor, token);
            ws.Audit.Record(actor, restarted ? "ApprovalCycleRestarted" : "InvoiceValidated", invoice.Reference, record.Id.ToString(),
                new { Overall = record.Overall.ToString(), record.RuleFingerprint });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    /// <summary>
    /// Atomic Submit (spec §5.3): evaluate the draft; Hard Stop means refusal without holds; otherwise reservations, claims,
    /// billing claims, the status and a re-evaluation in Submitted status (its outcomes are the basis for overrides), all in one transaction.
    /// </summary>
    public Task<CommandResult<InvoiceVm>> SubmitAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "SubmitInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.CreatedBy != actor.UserId || !actor.IsInRole(Roles.ApClerk))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the author submits the invoice.");
            }

            if (invoice.Status != InvoiceStatus.Draft)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"Invoice is {invoice.Status}.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var (draft, _) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Submit, actor, token);
            if (!draft.Capabilities.CanSubmit)
            {
                ws.Audit.Record(actor, "InvoiceSubmitRefused", invoice.Reference, draft.Id.ToString(), new { Overall = draft.Overall.ToString() });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), InvoiceWorkspace.ReasonOf(draft, Severity.HardStop));
            }

            var holds = await ws.HoldAsync(invoice, draft.InputSnapshot, token);
            invoice.Submit(draft.Id, draft.RuleFingerprint, await ws.RouteForAsync(draft, token), holds.Reservations, holds.Claims, holds.BillingClaims);
            var (submitted, _) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Submit, actor, token);
            ws.Audit.Record(actor, "InvoiceSubmitted", invoice.Reference, submitted.Id.ToString(),
                new
                {
                    Overall = submitted.Overall.ToString(), invoice.ContentVersion, invoice.ApprovalCycleId,
                    Reservations = holds.Reservations.Count, Claims = holds.Claims.Count, BillingClaims = holds.BillingClaims.Count,
                });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> WithdrawAsync(ReasonedActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "WithdrawInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var release = invoice.Withdraw(actor.UserId, cmd.Reason, ws.Clock.Now);   // not the author / not active → PayablesException → Refused
            await ws.ReleaseAsync(release, token);
            ws.Audit.Record(actor, "InvoiceWithdrawn", invoice.Reference, cmd.Envelope.CommandId.ToString(), new { cmd.Reason });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> ReturnToDraftAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "ReturnToDraft", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.CreatedBy != actor.UserId)
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the author reopens a rejected invoice.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            invoice.ReturnToDraft();   // not Rejected → PayablesException → Refused
            ws.Audit.Record(actor, "InvoiceReturnedToDraft", invoice.Reference, cmd.Envelope.CommandId.ToString(), new { invoice.ContentVersion });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> SetPaymentHoldAsync(PaymentHoldCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "SetPaymentHold", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Posters))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the budget officer or finance director sets a payment hold.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            invoice.SetPaymentHold(cmd.Hold);
            ws.Audit.Record(actor, "PaymentHoldChanged", invoice.Reference, cmd.Envelope.CommandId.ToString(), new { cmd.Hold });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    /// <summary>
    /// Refusals a new draft can get before anything changes. A duplicate of a hand-entered vendor number is caught here;
    /// a race of two identical numbers is closed by the unique index (runner → Refused).
    /// </summary>
    private static async Task<string?> NewDraftProblemAsync(InvoiceWorkspace ws, CreateInvoiceCommand cmd, CancellationToken ct)
    {
        if (DemoDateMismatch(cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate) is { } dates) return dates;
        _ = await ws.Vendors.FindAsync(cmd.VendorId, ct) ?? throw new NotFoundException($"Vendor {cmd.VendorId} not found.");
        if (cmd.PoRef is not null && await ws.PurchaseOrders.FindByNumberAsync(cmd.PoRef, ct) is null) return $"Purchase order {cmd.PoRef} not found.";
        if (cmd.GeneratedNumberPrefix is null
            && await ws.Invoices.ExistsDuplicateAsync(cmd.VendorId, VendorInvoice.NormalizeNumber(cmd.Number), null, ct))
        {
            return InvoiceRegistration.DuplicateNumber;
        }

        return null;
    }

    private static string? DemoDateMismatch(DateOnly invoiceDate, DateOnly serviceDate, DateOnly postingDate) =>
        invoiceDate == serviceDate && serviceDate == postingDate
            ? null
            : "Demo limitation: InvoiceDate, ServiceDate and PostingDate must be the same date.";
}
