using GovErp.Application.Web.Approvals.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using ApproverRole = GovErp.Domain.Validation.ValueObjects.ApproverRole;

namespace GovErp.Application.Web.Approvals;

public sealed class ApprovalAppService(ITenantOperationRunner runner) : IApprovalAppService
{
    public Task<IReadOnlyList<ApprovalQueueItemVm>> GetQueueAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<ApprovalQueueItemVm>>(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var items = new List<ApprovalQueueItemVm>();
            bool Mine(string role, string? department) => actor.IsInRole(role) && (department is null || department == actor.DepartmentCode);
            foreach (var inv in (await ws.Invoices.ListAsync(token)).Where(i => i.Status is InvoiceStatus.Submitted or InvoiceStatus.Approved))
            {
                if (await ws.LastEvaluationAsync(inv, token) is not { } last)
                {
                    continue;
                }

                var vendor = (await ws.Vendors.FindAsync(inv.VendorId, token))?.Name ?? "?";
                if (inv.Status == InvoiceStatus.Submitted && inv.CreatedBy != actor.UserId)
                {
                    var satisfied = last.ApprovalRoute.Where(r => r.IsSatisfied).Select(r => (r.Role.ToString(), r.Department)).ToHashSet();
                    items.AddRange((await ws.RouteForAsync(last, token))
                        .Select(r => (Role: r.Role.ToString(), Department: r.Department?.Value))
                        .Where(r => !satisfied.Contains(r) && Mine(r.Role, r.Department))
                        .Select(r => new ApprovalQueueItemVm(inv.Id, inv.Reference, vendor, inv.Total.Amount, last.Overall.ToString(),
                            "Approve", r.Role, r.Department, "Approval required.", last.Id, null, null)));
                }

                items.AddRange(last.Outcomes
                    .Where(o => o.Severity == Severity.SoftStop && !o.IsOverridden && inv.CreatedBy != actor.UserId)
                    .SelectMany(o => o.OverridableBy.Where(r => Mine(r.ToString(), null)).Take(1).Select(r => (o, r)))
                    .Select(x => new ApprovalQueueItemVm(inv.Id, inv.Reference, vendor, inv.Total.Amount, last.Overall.ToString(),
                        "Override", x.r.ToString(), null, x.o.Message, last.Id, x.o.RuleId, x.o.DistributionLine)));
            }

            return items;
        }, ct);

    /// <summary>
    /// Spec §5, правило 2: перед согласованием — оценка; смена fingerprint или маршрута уже открыла новый цикл внутри
    /// EvaluateAsync. Неснятый Soft Stop или Hard Stop блокирует (CanApprove). Согласование пишется по шагу базового маршрута.
    /// </summary>
    public Task<CommandResult<InvoiceVm>> ApproveAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "ApproveInvoice", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Approvers))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only approvers approve.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.CreatedBy == actor.UserId)
            {
                return CommandResult<InvoiceVm>.Forbidden("The author cannot approve their own invoice (separation of duties).");
            }

            if (invoice.Status != InvoiceStatus.Submitted)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"Invoice is {invoice.Status}.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var (record, restarted) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Approve, actor, token);
            if (restarted)
            {
                ws.Audit.Record(actor, "ApprovalCycleRestarted", invoice.Reference, record.Id.ToString(), new { invoice.ApprovalCycleId, record.RuleFingerprint });
            }

            if (!record.Capabilities.CanApprove)
            {
                ws.Audit.Record(actor, "ApprovalRefused", invoice.Reference, record.Id.ToString(), new { Overall = record.Overall.ToString() });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), InvoiceWorkspace.ReasonOf(record));
            }

            var route = await ws.RouteForAsync(record, token);
            var satisfied = record.ApprovalRoute.Where(r => r.IsSatisfied).Select(r => (r.Role.ToString(), r.Department)).ToHashSet();
            var pending = route.Where(r => !satisfied.Contains((r.Role.ToString(), r.Department?.Value))).ToList();
            var step = pending.FirstOrDefault(r => actor.IsInRole(r.Role.ToString()) && (r.Department is null || r.Department.Value == actor.DepartmentCode));
            if (step is null)
            {
                return CommandResult<InvoiceVm>.Forbidden($"{actor.UserName} is not a pending approver for {invoice.Reference}.");
            }

            invoice.RecordApproval(step.Role, step.Department, actor.UserId, record.Id, ws.Clock.Now);
            if (pending.Count == 1)
            {
                invoice.MarkApproved();
            }

            ws.Audit.Record(actor, "InvoiceApproved", invoice.Reference, record.Id.ToString(),
                new { Role = step.Role.ToString(), Department = step.Department?.Value, invoice.ApprovalCycleId, Status = invoice.Status.ToString() });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> RejectAsync(ReasonedActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "RejectInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (await ws.LastEvaluationAsync(invoice, token) is not { } last || invoice.LastEvaluationRef is null)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"Invoice is {invoice.Status}.");
            }

            var step = (await ws.RouteForAsync(last, token))
                .FirstOrDefault(r => actor.IsInRole(r.Role.ToString()) && (r.Department is null || r.Department.Value == actor.DepartmentCode));
            if (step is null)
            {
                return CommandResult<InvoiceVm>.Forbidden("Only an approver on this invoice's route rejects it.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var release = invoice.Reject(step.Role, step.Department, actor.UserId, cmd.Reason, ws.Clock.Now);
            await ws.ReleaseAsync(release, token);
            ws.Audit.Record(actor, "InvoiceRejected", invoice.Reference, cmd.Envelope.CommandId.ToString(),
                new { cmd.Reason, Role = step.Role.ToString(), Department = step.Department?.Value });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    /// <summary>
    /// Override выдаётся только на outcome последней оценки инвойса (spec §4.3, OutcomeRef): чужую или устаревшую
    /// оценку домен не примет. После override — переоценка: её результат — новая последняя оценка.
    /// </summary>
    public Task<CommandResult<InvoiceVm>> OverrideAsync(OverrideCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "OverrideRule", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Overriders))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the budget officer or finance director overrides.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.LastEvaluationRef != cmd.EvaluationId || await ws.Evaluations.FindAsync(cmd.EvaluationId, token) is not { } basis)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), "The evaluation is not current for this invoice; reload and retry.");
            }

            var outcome = basis.Outcomes.FirstOrDefault(o => o.RuleId == cmd.RuleId && o.DistributionLine == cmd.DistributionLine
                && o.Severity == Severity.SoftStop && !o.IsOverridden);
            if (outcome is null)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"No open soft stop {cmd.RuleId} on that line.");
            }

            // Cast к nullable: FirstOrDefault по enum вернул бы DepartmentHead вместо «нет совпадения».
            if (outcome.OverridableBy.Cast<ApproverRole?>().FirstOrDefault(r => actor.IsInRole(r!.Value.ToString())) is not { } role)
            {
                return CommandResult<InvoiceVm>.Forbidden($"{cmd.RuleId} cannot be overridden by {actor.UserName}.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            invoice.Override(new OverrideTarget(basis.Id, outcome.OutcomeRef, outcome.RuleId, outcome.RuleVersion, outcome.DistributionLine,
                invoice.ContentVersion, invoice.ApprovalCycleId!.Value), RoleMapping.ToPayables(role), actor.UserId, cmd.Reason, ws.Clock.Now);
            var (record, _) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Manual, actor, token);
            ws.Audit.Record(actor, "OverrideRecorded", invoice.Reference, record.Id.ToString(),
                new { outcome.RuleId, outcome.RuleVersion, outcome.DistributionLine, Role = role.ToString(), cmd.Reason, Overall = record.Overall.ToString() });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);
}
