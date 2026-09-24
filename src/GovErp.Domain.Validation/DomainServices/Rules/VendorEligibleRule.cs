using GovErp.Domain.Validation.DomainServices.RuleSupport;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Step 4. The vendor is active and not debarred; payments from a federal grant also need SAM registration (demo policy).
/// </summary>
public sealed class VendorEligibleRule : ValidationRule
{
    private const string VendorInactive = "VENDOR_ELIGIBLE.VENDOR_INACTIVE";
    private const string VendorDebarred = "VENDOR_ELIGIBLE.VENDOR_DEBARRED";
    private const string SamRegistrationRequired = "VENDOR_ELIGIBLE.SAM_REGISTRATION_REQUIRED";

    public override string RuleId => "VENDOR_ELIGIBLE";

    protected override IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters)
    {
        // Scope: the invoice's vendor, and whether any line is paid from a federal grant.
        var vendor = subject.Transaction.Vendor;
        var federalGrant = subject.Distributions.Any(d => d.Grant is { IsFederal: true });

        // Decide: the first reason the vendor may not be paid.
        Verdict verdict;
        if (!vendor.IsActive)
            verdict = Fail(VendorInactive);
        else if (vendor.IsDebarred)
            verdict = Fail(VendorDebarred);
        else if (federalGrant && !vendor.SamRegistered)
            verdict = Fail(SamRegistrationRequired);
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
