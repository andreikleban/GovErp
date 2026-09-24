namespace GovErp.Application.Web.Audit;

/// <summary>SubjectInvoiceId: SubjectRef resolved to an invoice when it is that invoice's Reference (spec: "the subject refers to a document"); otherwise null.</summary>
public sealed record AuditEventVm(DateTimeOffset OccurredAt, string ActorName, string Action, string SubjectRef, string CorrelationId,
    string PayloadJson, Guid? SubjectInvoiceId = null)
{
    public static AuditEventVm From(AuditEvent e, Guid? subjectInvoiceId = null) =>
        new(e.OccurredAt, e.ActorName, e.Action, e.SubjectRef, e.CorrelationId, e.PayloadJson, subjectInvoiceId);
}
