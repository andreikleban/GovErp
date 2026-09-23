using GovErp.Application.Web.Validation.Contracts;

namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>
/// RowVersion и Funds заполнены только в ответах чтения (GetAsync): после команды UI перечитывает инвойс, а резервы,
/// созданные ещё не сохранённой командой, запросом к хранилищу не видны.
/// </summary>
public sealed record InvoiceVm(Guid Id, string Number, string Reference, Guid VendorId, string VendorName,
    DateOnly InvoiceDate, DateOnly ServiceDate, DateOnly PostingDate, DateOnly DueDate, int FiscalYear, decimal Total, string? PoRef,
    string Status, int ContentVersion, Guid? ApprovalCycleId, Guid? LastEvaluationRef, string? RowVersion, Guid CreatedBy, bool PaymentHold,
    bool ReadyForPaymentHandoff, DateTimeOffset? PostedAt, InvoiceFundsVm? Funds, IReadOnlyList<DistributionVm> Distributions,
    IReadOnlyList<ApprovalVm> Approvals, IReadOnlyList<OverrideVm> Overrides, EvaluationVm? LastEvaluation);
