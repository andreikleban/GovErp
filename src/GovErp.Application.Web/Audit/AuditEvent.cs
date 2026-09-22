namespace GovErp.Application.Web.Audit;

public sealed record AuditEvent(Guid Id, TenantId TenantId, DateTimeOffset OccurredAt, UserId Actor, string ActorName,
    string Action, string SubjectRef, string CorrelationId, string PayloadJson);
