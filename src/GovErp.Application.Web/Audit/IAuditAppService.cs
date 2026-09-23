using GovErp.Application.Web.Audit.Contracts;
using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Audit;

public interface IAuditAppService
{
    /// <summary>Последние 200 оценок тенанта по всем документам, после фильтров.</summary>
    Task<IReadOnlyList<EvaluationListItemVm>> ListEvaluationsAsync(EvaluationFilter filter, ActorContext actor, CancellationToken ct = default);
    /// <summary>Последние 200 событий аудита тенанта, после фильтров.</summary>
    Task<IReadOnlyList<AuditEventVm>> ListEventsAsync(EventFilter filter, ActorContext actor, CancellationToken ct = default);
}
