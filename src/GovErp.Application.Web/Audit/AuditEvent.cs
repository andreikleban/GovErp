namespace GovErp.Application.Web.Audit;

/// <summary>
/// One entry in the user action log.
/// </summary>
public sealed record AuditEvent(Guid Id, TenantId TenantId, DateTimeOffset OccurredAt, UserId Actor, string ActorName,
    string Action, string SubjectRef, string CorrelationId, string PayloadJson);
