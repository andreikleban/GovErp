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
/// Сценарии инвойса. Правило для всех команд: возвращённый Refused / Forbidden сохраняет изменения тела вместе с receipt
/// (запись оценки, аудит отказа, новый цикл согласования); отказ после изменения агрегатов, которое нельзя сохранять,
/// оформляется только доменным исключением — runner превращает его в Refused и откатывает всё.
/// </summary>
public sealed class InvoiceAppService(ITenantOperationRunner runner) : IInvoiceAppService
{
    public Task<IReadOnlyList<InvoiceListItemVm>> ListAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<InvoiceListItemVm>>(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var result = new List<InvoiceListItemVm>();
            foreach (var inv in (await ws.Invoices.ListAsync(token)).OrderByDescending(i => i.CreatedAt))
            {
                var vendor = await ws.Vendors.FindAsync(inv.VendorId, token);
                var last = await ws.LastEvaluationAsync(inv, token);
                result.Add(InvoiceMapping.ToListItem(inv, vendor?.Name ?? "?", last?.Overall.ToString()));
            }

            return result;
        }, ct);

    public Task<InvoiceVm> GetAsync(Guid id, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            return await ws.ToVmAsync(await ws.LoadAsync(id, token), token, withRowVersion: true);
        }, ct);

    public Task<CommandResult<InvoiceVm>> CreateDraftAsync(CreateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "CreateInvoice", cmd, async (sp, token) =>
        {
            if (!actor.IsInRole(Roles.ApClerk))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only AP clerks create invoices.");
            }

            if (DemoDateMismatch(cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate) is { } createDateReason)
            {
                return CommandResult<InvoiceVm>.Refused(null, createDateReason);
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            _ = await ws.Vendors.FindAsync(cmd.VendorId, token) ?? throw new NotFoundException($"Vendor {cmd.VendorId} not found.");
            if (cmd.PoRef is not null && await ws.PurchaseOrders.FindByNumberAsync(cmd.PoRef, token) is null)
            {
                return CommandResult<InvoiceVm>.Refused(null, $"Purchase order {cmd.PoRef} not found.");
            }

            // Конструктор и AddDistribution проверяют инварианты; даты вне правила 6 отклонит конвейер (VALIDATION_INPUT).
            var invoice = new VendorInvoice(cmd.Number, cmd.VendorId, cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate, cmd.DueDate,
                Money.Of(cmd.Total), cmd.PoRef, actor.UserId, ws.Clock.Now);
            foreach (var d in cmd.Distributions)
            {
                invoice.AddDistribution(AccountCode.Parse(d.Account), Money.Of(d.Amount), d.PoLineNo);
            }

            if (await ws.Invoices.ExistsDuplicateAsync(invoice.VendorId, invoice.NormalizedInvoiceNumber, null, token))
            {
                return CommandResult<InvoiceVm>.Refused(null, "An invoice with this number already exists for this vendor.");
            }

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
                Money.Of(cmd.Total), cmd.PoRef);   // не Draft → PayablesException → Refused
            var wanted = cmd.Distributions.Select(d => (AccountCode.Parse(d.Account), Money.Of(d.Amount), d.PoLineNo)).ToList();
            if (!invoice.Distributions.Select(d => (d.Account, d.Amount, d.PoLineNo)).SequenceEqual(wanted))
            {
                for (var n = invoice.Distributions.Count; n >= 1; n--)
                {
                    invoice.RemoveDistribution(n);
                }

                foreach (var (account, amount, poLine) in wanted)
                {
                    invoice.AddDistribution(account, amount, poLine);
                }
            }

            if (await ws.Invoices.ExistsDuplicateAsync(invoice.VendorId, invoice.NormalizedInvoiceNumber, invoice.Id, token))
            {
                // Агрегат уже изменён: отказ только исключением, чтобы runner откатил изменения.
                throw new PayablesException("An invoice with this number already exists for this vendor.");
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
    /// Атомарный Submit (spec §5.3): оценка черновика; Hard Stop — отказ без удержаний; иначе резервы, claims,
    /// billing claims, статус и повторная оценка в статусе Submitted (её outcome'ы — основа для override) — одна транзакция.
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
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), InvoiceWorkspace.ReasonOf(draft));
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
            var release = invoice.Withdraw(actor.UserId, cmd.Reason, ws.Clock.Now);   // не автор / не активен → PayablesException → Refused
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
            invoice.ReturnToDraft();   // не Rejected → PayablesException → Refused
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

    private static string? DemoDateMismatch(DateOnly invoiceDate, DateOnly serviceDate, DateOnly postingDate) =>
        invoiceDate == serviceDate && serviceDate == postingDate
            ? null
            : "Demo limitation: InvoiceDate, ServiceDate and PostingDate must be the same date.";
}
