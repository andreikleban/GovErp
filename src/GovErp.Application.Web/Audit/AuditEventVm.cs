namespace GovErp.Application.Web.Audit;

public sealed record AuditEventVm(DateTimeOffset OccurredAt, string ActorName, string Action, string SubjectRef, string CorrelationId, string PayloadJson)
{
    public static AuditEventVm From(AuditEvent e) => new(e.OccurredAt, e.ActorName, e.Action, e.SubjectRef, e.CorrelationId, e.PayloadJson);
}
