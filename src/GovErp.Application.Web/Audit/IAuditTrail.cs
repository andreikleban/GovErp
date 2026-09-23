using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Audit;

public interface IAuditTrail
{
    void Record(ActorContext actor, string action, string subjectRef, string correlationId, object payload);
    Task<IReadOnlyList<AuditEvent>> ListBySubjectAsync(string subjectRef, CancellationToken ct = default);
    /// <summary>Все события тенанта, без сортировки и ограничения — Audit сам берёт latest 200 после фильтров.</summary>
    Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken ct = default);
}
