using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Validation;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Application.Web.Invoices.Mapping;

/// <summary>
/// Builds an invoice view from the aggregate and its evaluation.
/// </summary>
public static class InvoiceMapping
{
    public static InvoiceVm ToVm(VendorInvoice invoice, Vendor vendor, EvaluationRecord? last, string? rowVersion, DateOnly businessDate,
        InvoiceFundsVm? funds) => new(
        invoice.Id, invoice.Number, invoice.Reference, invoice.VendorId, vendor.Name,
        invoice.InvoiceDate, invoice.ServiceDate, invoice.PostingDate, invoice.DueDate, FiscalYear.FromDate(invoice.PostingDate).Year,
        invoice.Total.Amount, invoice.PoRef,
        invoice.Status.ToString(), invoice.ContentVersion, invoice.ApprovalCycleId, invoice.LastEvaluationRef, rowVersion,
        invoice.CreatedBy.Value, invoice.PaymentHold,
        invoice.ReadyForPaymentHandoff(vendor.Status == VendorStatus.Active, businessDate), invoice.PostedAt, invoice.PaidAt, funds,
        invoice.Distributions.Select(d => new DistributionVm(d.LineNo, d.Account.ToString(), d.Amount.Amount, d.PoLineNo)).ToList(),
        invoice.Approvals.Select(a => new ApprovalVm(a.ApprovalCycleId,
            a.ApprovalCycleId == invoice.ApprovalCycleId && a.ContentVersion == invoice.ContentVersion,
            a.ContentVersion, a.Role.ToString(), a.Department?.Value, a.UserId.Value, a.Decision.ToString(), a.Reason, a.At)).ToList(),
        invoice.Overrides.Select(o => new OverrideVm(o.Target.ApprovalCycleId, invoice.HasCurrentOverride(o.Target), o.Target.EvaluationRef,
            o.Target.OutcomeRef, o.Target.RuleId, o.Target.RuleVersion, o.Target.DistributionLine, o.Role.ToString(), o.UserId.Value,
            o.Reason, o.At)).ToList(),
        last is null ? null : EvaluationMapping.ToVm(last));

    public static InvoiceListItemVm ToListItem(VendorInvoice invoice, string vendorName, EvaluationRecord? last) =>
        new(invoice.Id, invoice.Reference, invoice.Number, vendorName, invoice.Total.Amount, invoice.Status.ToString(), last?.Overall.ToString(),
            invoice.PostingDate, FundsOf(invoice), OpenHolds(last));

    /// <summary>
    /// Fund codes of the lines in line order, without duplicates.
    /// </summary>
    public static IReadOnlyList<string> FundsOf(VendorInvoice invoice) =>
        invoice.Distributions.Select(d => d.Account.Fund.Value).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// A hold is an open Soft Stop or a Hard Stop of the evaluation; a Warning is not a hold.
    /// </summary>
    public static int OpenHolds(EvaluationRecord? last) =>
        last?.Outcomes.Count(o => o.Severity >= Severity.SoftStop && !o.IsOverridden) ?? 0;
}
