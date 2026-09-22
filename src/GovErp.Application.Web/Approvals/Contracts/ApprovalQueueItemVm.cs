namespace GovErp.Application.Web.Approvals.Contracts;

/// <summary>Kind = "Approve" (шаг базового маршрута) или "Override" (открытый Soft Stop, который актор вправе снять).</summary>
public sealed record ApprovalQueueItemVm(Guid InvoiceId, string Reference, string VendorName, decimal Total, string Overall, string Kind,
    string Role, string? Department, string Reason, Guid EvaluationId, string? RuleId, int? Line);
