using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Step 4. The vendor is active and not debarred; payments from a federal grant also need SAM registration (demo policy).</summary>
public sealed class VendorEligibleRule : ValidationRule
{
    public override string RuleId => "VENDOR_ELIGIBLE";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: the invoice's vendor, and whether any line is paid from a federal grant.
        var vendor = subject.Transaction.Vendor;
        var federalGrant = subject.Distributions.Any(d => d.Grant is { IsFederal: true });

        // Decide: the first reason the vendor may not be paid.
        Verdict verdict;
        if (!vendor.IsActive)
            verdict = Fail($"Vendor {vendor.Name} is inactive.");
        else if (vendor.IsDebarred)
            verdict = Fail($"Vendor {vendor.Name} is debarred from government contracts.");
        else if (federalGrant && !vendor.SamRegistered)
            verdict = Fail($"Demo policy requires vendor {vendor.Name} to be registered in SAM.gov for federal grant payments.");
        else
            yield break;

        // Evidence
        yield return verdict.OnInvoice()
            .InputAs("vendor", vendor.Name)
            .Input(vendor.VendorId)
            .InputAs("active", vendor.IsActive)
            .InputAs("debarred", vendor.IsDebarred)
            .Input(vendor.SamRegistered)
            .Computed(federalGrant);
    }
}
