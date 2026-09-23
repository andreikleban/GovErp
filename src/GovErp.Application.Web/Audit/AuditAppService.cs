using GovErp.Application.Web.Audit.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Tenancy;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Audit;

/// <summary>
/// Аудит на чтение по всему тенанту: оценки движка и события аудита за всеми документами (только инвойс — единственный
/// документ, который сегодня проходит через ValidationPipeline и IAuditTrail). Latest 200 — после фильтров, по времени убыв.
/// </summary>
public sealed class AuditAppService(ITenantOperationRunner runner) : IAuditAppService
{
    private const int Take = 200;

    public Task<IReadOnlyList<EvaluationListItemVm>> ListEvaluationsAsync(EvaluationFilter filter, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<EvaluationListItemVm>>(actor, async (sp, token) =>
        {
            var document = string.IsNullOrWhiteSpace(filter.Document) ? null : filter.Document.Trim();
            var trigger = string.IsNullOrWhiteSpace(filter.Trigger) ? null : filter.Trigger.Trim();
            var overall = string.IsNullOrWhiteSpace(filter.Overall) ? null : filter.Overall.Trim();
            var fingerprint = string.IsNullOrWhiteSpace(filter.RuleFingerprint) ? null : filter.RuleFingerprint.Trim();
            var names = await sp.GetRequiredService<ITenantUserDirectory>().ResolveAsync(actor.TenantId, token);
            var matching = (await sp.GetRequiredService<IEvaluationRecordRepository>().ListAsync(token))
                .Where(r => document is null || r.TransactionRef.Contains(document, StringComparison.OrdinalIgnoreCase))
                .Where(r => trigger is null || string.Equals(r.Trigger.ToString(), trigger, StringComparison.OrdinalIgnoreCase))
                .Where(r => overall is null || string.Equals(r.Overall.ToString(), overall, StringComparison.OrdinalIgnoreCase))
                .Where(r => fingerprint is null || r.RuleFingerprint.Contains(fingerprint, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.EvaluatedAt)
                .Take(Take);

            var invoices = sp.GetRequiredService<IVendorInvoiceRepository>();
            var result = new List<EvaluationListItemVm>();
            foreach (var r in matching)
            {
                var invoice = await invoices.FindByReferenceAsync(r.TransactionRef, token);
                result.Add(new EvaluationListItemVm(r.Id, r.EvaluatedAt, names.NameOf(r.EvaluatedBy.Value), invoice?.Id, r.TransactionRef,
                    r.Trigger.ToString(), r.TransactionVersion, r.Overall.ToString(), r.RuleFingerprint));
            }

            return result;
        }, ct);

    public Task<IReadOnlyList<AuditEventVm>> ListEventsAsync(EventFilter filter, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<AuditEventVm>>(actor, async (sp, token) =>
        {
            var action = string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action.Trim();
            var actorFilter = string.IsNullOrWhiteSpace(filter.Actor) ? null : filter.Actor.Trim();
            var subject = string.IsNullOrWhiteSpace(filter.Subject) ? null : filter.Subject.Trim();
            var matching = (await sp.GetRequiredService<IAuditTrail>().ListAsync(token))
                .Where(e => action is null || e.Action.Contains(action, StringComparison.OrdinalIgnoreCase))
                .Where(e => actorFilter is null || e.ActorName.Contains(actorFilter, StringComparison.OrdinalIgnoreCase))
                .Where(e => subject is null || e.SubjectRef.Contains(subject, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.OccurredAt)
                .Take(Take);

            // SubjectRef бывает разным по смыслу (Reference инвойса, счёт для BudgetAmended и т.п.): пробуем разрешить
            // как инвойс и кэшируем по значению, чтобы не бить репозиторий повторно на одинаковых subjectRef.
            var invoices = sp.GetRequiredService<IVendorInvoiceRepository>();
            var cache = new Dictionary<string, Guid?>(StringComparer.Ordinal);
            var result = new List<AuditEventVm>();
            foreach (var e in matching)
            {
                if (!cache.TryGetValue(e.SubjectRef, out var invoiceId))
                {
                    invoiceId = (await invoices.FindByReferenceAsync(e.SubjectRef, token))?.Id;
                    cache[e.SubjectRef] = invoiceId;
                }

                result.Add(AuditEventVm.From(e, invoiceId));
            }

            return result;
        }, ct);
}
