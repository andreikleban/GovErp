using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

public sealed class VendorEligibleRule : IValidationRule
{
    public string RuleId => "VENDOR_ELIGIBLE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var v = subject.Transaction.Vendor;
        if (v is null || v.VendorId == Guid.Empty)
        {
            return [RuleOutcome.From(definition, Severity.HardStop, null,
                RuleSupport.Map(("transactionRef", subject.Transaction.TransactionRef)),
                RuleSupport.Map(("missingFact", "vendor")), "Identified vendor facts are required.")];
        }

        var inputs = RuleSupport.Map(("vendor", v.Name), ("vendorId", v.VendorId.ToString()),
            ("active", v.IsActive.ToString()), ("debarred", v.IsDebarred.ToString()), ("samRegistered", v.SamRegistered.ToString()));
        if (!v.IsActive)
        {
            return [RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, null, inputs, RuleSupport.Map(),
                $"Vendor {v.Name} is inactive.")];
        }
        if (v.IsDebarred)
        {
            return [RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, null, inputs, RuleSupport.Map(),
                $"Vendor {v.Name} is debarred from government contracts.")];
        }

        if (subject.Distributions.Any(d => d.Account.Grant is not null && d.Grant is null))
        {
            return [RuleOutcome.From(definition, Severity.HardStop, null, inputs,
                RuleSupport.Map(("missingFact", "grant")),
                "Grant facts are required to determine vendor SAM eligibility.")];
        }

        var touchesFederalGrant = subject.Distributions.Any(d => d.Grant is { IsFederal: true });
        if (touchesFederalGrant && !v.SamRegistered)
        {
            return [RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, null, inputs, RuleSupport.Map(("federalGrant", "true")),
                $"Demo policy requires vendor {v.Name} to be registered in SAM.gov for federal grant payments.")];
        }

        return [];
    }
}

