using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Audit;

public interface IAuditTrail
{
    void Record(ActorContext actor, string action, string subjectRef, string correlationId, object payload);
    Task<IReadOnlyList<AuditEvent>> ListBySubjectAsync(string subjectRef, CancellationToken ct = default);
    /// <summary>All tenant events, unsorted and unlimited; Audit itself takes the latest 200 after filters.</summary>
    Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken ct = default);
}
