namespace GovErp.Application.Web.Audit.Contracts;

/// <summary>
/// A row of "Audit › Evaluations" across all documents. DocumentId is the invoice with Reference == TransactionRef if it is still visible
/// (only invoices go through the engine: PostingAppService and InvoiceWorkspace are the only callers of ValidationPipeline);
/// null is a fallback in case the document is no longer visible. RuleFingerprint is full (the UI shows it shortened, title shows it in full).
/// </summary>
public sealed record EvaluationListItemVm(Guid Id, DateTimeOffset EvaluatedAt, string ActorName, Guid? DocumentId, string DocumentReference,
    string Trigger, int ContentVersion, string Overall, string RuleFingerprint);
