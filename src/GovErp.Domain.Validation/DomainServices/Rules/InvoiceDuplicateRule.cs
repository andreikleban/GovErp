using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class InvoiceDuplicateRule : IValidationRule
{
    public string RuleId => "INVOICE_DUPLICATE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Transaction.IsDuplicate
            ? [RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, null,
                RuleSupport.Map(("vendor", subject.Transaction.Vendor.Name), ("transactionRef", subject.Transaction.TransactionRef)),
                RuleSupport.Map(),
                $"An invoice with the same number from {subject.Transaction.Vendor.Name} already exists.")]
            : [];
}

