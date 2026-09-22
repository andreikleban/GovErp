using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Validation;
using GovErp.Application.Web.Validation.Contracts;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Explanation;

/// <summary>Объяснения оценок. Генерация (сеть, LLM) — до и вне транзакции (consistency §4); транзакция только сохраняет результат.</summary>
public sealed class ExplanationAppService(ITenantOperationRunner runner, IExplanationGenerator generator) : IExplanationAppService
{
    public async Task<CommandResult<ExplanationVm>> ExplainAsync(Guid evaluationId, ExplanationAudience audience, CommandEnvelope envelope,
        ActorContext actor, CancellationToken ct = default)
    {
        var record = await runner.QueryAsync(actor, (sp, token) => sp.GetRequiredService<IEvaluationRecordRepository>().FindAsync(evaluationId, token), ct);
        if (record is null)
        {
            return CommandResult<ExplanationVm>.NotFound($"Evaluation {evaluationId} not found.");
        }

        var result = await generator.ExplainAsync(record, audience, ct);   // сеть — до и вне транзакции
        return await runner.ExecuteAsync(actor, envelope, "ExplainEvaluation", new { evaluationId, audience }, (sp, token) =>
        {
            var clock = sp.GetRequiredService<IClock>();
            var saved = new ExplanationRecord(Guid.NewGuid(), record.Id, record.TransactionRef, audience, result.Text, result.Provider,
                result.Model, result.PromptVersion, result.FallbackReason, clock.Now);
            sp.GetRequiredService<IExplanationRepository>().Add(saved);
            sp.GetRequiredService<IAuditTrail>().Record(actor, "ExplanationGenerated", record.TransactionRef, record.Id.ToString(),
                new { audience = audience.ToString(), result.Provider, result.Model, result.FallbackReason });
            return Task.FromResult(CommandResult<ExplanationVm>.Accepted(ExplanationMapping.ToVm(saved)));
        }, ct: ct);
    }

    public Task<IReadOnlyList<ExplanationVm>> ListAsync(Guid evaluationId, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<ExplanationVm>>(actor, async (sp, token) =>
            (await sp.GetRequiredService<IExplanationRepository>().ListByEvaluationAsync(evaluationId, token))
                .Select(ExplanationMapping.ToVm).ToList(), ct);

    public Task<IReadOnlyList<EvaluationVm>> GetEvaluationHistoryAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<EvaluationVm>>(actor, async (sp, token) =>
        {
            var reference = await ReferenceOfAsync(sp, invoiceId, token);
            return (await sp.GetRequiredService<IEvaluationRecordRepository>().ListByTransactionAsync(reference, token))
                .Select(EvaluationMapping.ToVm).ToList();
        }, ct);

    public Task<IReadOnlyList<AuditEventVm>> GetAuditTrailAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<AuditEventVm>>(actor, async (sp, token) =>
        {
            var reference = await ReferenceOfAsync(sp, invoiceId, token);
            return (await sp.GetRequiredService<IAuditTrail>().ListBySubjectAsync(reference, token)).Select(AuditEventVm.From).ToList();
        }, ct);

    private static async Task<string> ReferenceOfAsync(IServiceProvider sp, Guid invoiceId, CancellationToken ct) =>
        (await sp.GetRequiredService<IVendorInvoiceRepository>().FindAsync(invoiceId, ct) ?? throw new NotFoundException($"Invoice {invoiceId} not found."))
            .Reference;
}
