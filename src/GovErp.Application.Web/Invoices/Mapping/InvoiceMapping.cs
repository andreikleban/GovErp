using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Validation;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.Entities;

namespace GovErp.Application.Web.Invoices.Mapping;

public static class InvoiceMapping
{
    public static InvoiceVm ToVm(VendorInvoice invoice, Vendor vendor, EvaluationRecord? last, string? rowVersion, DateOnly businessDate) => new(
        invoice.Id, invoice.Number, invoice.Reference, invoice.VendorId, vendor.Name,
        invoice.InvoiceDate, invoice.ServiceDate, invoice.PostingDate, invoice.DueDate, invoice.Total.Amount, invoice.PoRef,
        invoice.Status.ToString(), invoice.ContentVersion, invoice.ApprovalCycleId, invoice.LastEvaluationRef, rowVersion,
        invoice.CreatedBy.Value, invoice.PaymentHold,
        invoice.ReadyForPaymentHandoff(vendor.Status == VendorStatus.Active, businessDate),
        invoice.Distributions.Select(d => new DistributionVm(d.LineNo, d.Account.ToString(), d.Amount.Amount, d.PoLineNo)).ToList(),
        invoice.Approvals.Select(a => new ApprovalVm(a.ApprovalCycleId,
            a.ApprovalCycleId == invoice.ApprovalCycleId && a.ContentVersion == invoice.ContentVersion,
            a.ContentVersion, a.Role.ToString(), a.Department?.Value, a.UserId.Value, a.Decision.ToString(), a.Reason, a.At)).ToList(),
        invoice.Overrides.Select(o => new OverrideVm(o.Target.ApprovalCycleId, invoice.HasCurrentOverride(o.Target), o.Target.EvaluationRef,
            o.Target.OutcomeRef, o.Target.RuleId, o.Target.RuleVersion, o.Target.DistributionLine, o.Role.ToString(), o.UserId.Value,
            o.Reason, o.At)).ToList(),
        last is null ? null : EvaluationMapping.ToVm(last));

    public static InvoiceListItemVm ToListItem(VendorInvoice invoice, string vendorName, string? lastOverall) =>
        new(invoice.Id, invoice.Reference, vendorName, invoice.Total.Amount, invoice.Status.ToString(), lastOverall, invoice.PostingDate);
}
