using System.Text.Json;
using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Tenancy;
using GovErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Audit;

public sealed class EfAuditTrail(GovErpDbContext db, ITenantContext tenant, IClock clock) : IAuditTrail
{
    public void Record(ActorContext actor, string action, string subjectRef, string correlationId, object payload) =>
        db.AuditEvents.Add(new AuditEvent(Guid.NewGuid(), tenant.TenantId, clock.Now, actor.UserId, actor.UserName, action, subjectRef,
            correlationId, JsonSerializer.Serialize(payload, JsonColumn.Options)));

    public async Task<IReadOnlyList<AuditEvent>> ListBySubjectAsync(string subjectRef, CancellationToken ct = default) =>
        await db.AuditEvents.Where(e => e.SubjectRef == subjectRef).OrderBy(e => e.OccurredAt).ToListAsync(ct);
}
