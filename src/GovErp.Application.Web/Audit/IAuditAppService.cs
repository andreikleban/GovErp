using GovErp.Application.Web.Audit.Contracts;
using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Audit;

/// <summary>
/// Reads the action log and the stored evaluations.
/// </summary>
public interface IAuditAppService
{
    /// <summary>
    /// The tenant's latest 200 evaluations across all documents, after filters.
    /// </summary>
    Task<IReadOnlyList<EvaluationListItemVm>> ListEvaluationsAsync(EvaluationFilter filter, ActorContext actor, CancellationToken ct = default);
    /// <summary>
    /// The tenant's latest 200 audit events, after filters.
    /// </summary>
    Task<IReadOnlyList<AuditEventVm>> ListEventsAsync(EventFilter filter, ActorContext actor, CancellationToken ct = default);
}
