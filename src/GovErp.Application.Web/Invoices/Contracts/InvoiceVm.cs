using GovErp.Application.Web.Validation.Contracts;

namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>
/// RowVersion and Funds are filled only in read responses (GetAsync): after a command the UI re-reads the invoice, and reservations
/// created by a command that is not yet saved are not visible to a storage query.
/// </summary>
public sealed record InvoiceVm(Guid Id, string Number, string Reference, Guid VendorId, string VendorName,
    DateOnly InvoiceDate, DateOnly ServiceDate, DateOnly PostingDate, DateOnly DueDate, int FiscalYear, decimal Total, string? PoRef,
    string Status, int ContentVersion, Guid? ApprovalCycleId, Guid? LastEvaluationRef, string? RowVersion, Guid CreatedBy, bool PaymentHold,
    bool ReadyForPaymentHandoff, DateTimeOffset? PostedAt, DateTimeOffset? PaidAt, InvoiceFundsVm? Funds, IReadOnlyList<DistributionVm> Distributions,
    IReadOnlyList<ApprovalVm> Approvals, IReadOnlyList<OverrideVm> Overrides, EvaluationVm? LastEvaluation);
