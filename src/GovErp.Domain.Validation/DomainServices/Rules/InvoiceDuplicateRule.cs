using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Step 4. Another invoice from the same vendor with the same normalized number already exists.</summary>
public sealed class InvoiceDuplicateRule : ValidationRule
{
    public override string RuleId => "INVOICE_DUPLICATE";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: the invoice as a whole (the snapshot assembler looked up the vendor's other invoices).
        var invoice = subject.Transaction;

        // Decide: a possible duplicate payment.
        Verdict verdict;
        if (invoice.IsDuplicate)
            verdict = Fail($"An invoice with the same number from {invoice.Vendor.Name} already exists.");
        else
            yield break;

        // Evidence
        yield return verdict.OnInvoice()
            .InputAs("vendor", invoice.Vendor.Name)
            .Input(invoice.TransactionRef);
    }
}
