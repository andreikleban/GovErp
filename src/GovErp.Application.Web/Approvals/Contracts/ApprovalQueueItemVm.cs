namespace GovErp.Application.Web.Approvals.Contracts;

/// <summary>
/// Kind = "Approve" (a base route step) or "Override" (an open Soft Stop the actor may release).
/// </summary>
public sealed record ApprovalQueueItemVm(Guid InvoiceId, string Reference, string Number, string VendorName, decimal Total, string Overall, string Kind,
    string Role, string? Department, string Reason, Guid EvaluationId, string? RuleId, int? Line);
