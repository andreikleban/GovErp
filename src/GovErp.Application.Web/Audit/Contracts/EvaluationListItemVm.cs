namespace GovErp.Application.Web.Audit.Contracts;

/// <summary>
/// Строка списка «Audit › Evaluations» по всем документам. DocumentId — инвойс с Reference == TransactionRef, если он ещё виден
/// (только инвойсы проходят через движок — PostingAppService и InvoiceWorkspace единственные вызывающие ValidationPipeline);
/// null — заглушка на случай, если документ исчез из вида. RuleFingerprint — полный (в UI показан сокращённым, title — полный).
/// </summary>
public sealed record EvaluationListItemVm(Guid Id, DateTimeOffset EvaluatedAt, string ActorName, Guid? DocumentId, string DocumentReference,
    string Trigger, int ContentVersion, string Overall, string RuleFingerprint);
