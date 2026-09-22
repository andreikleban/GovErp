using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public static class PostingEligibility
{
    public static PostingCheck Check(ValidationSubject subject, Severity? overall,
        IReadOnlyList<ApprovalRequirement>? route, PostingPreview? preview, EffectiveRuleSet effectiveRules)
    {
        var failures = new List<string>();
        if (subject.Transaction.Status != "Approved") failures.Add("Only Approved invoices can post.");
        if (overall is not (Severity.Allowed or Severity.Warning)) failures.Add("Validation is missing or has unresolved stops.");
        if (!subject.PeriodIsOpen) failures.Add("Posting period is closed.");
        if (string.IsNullOrWhiteSpace(effectiveRules.Fingerprint) || subject.Transaction.RuleFingerprint != effectiveRules.Fingerprint
            || subject.VersionsAtLastApproval is { Fingerprint.Length: > 0 } approved && approved.Fingerprint != effectiveRules.Fingerprint)
            failures.Add("REVALIDATION_REQUIRED: current rule fingerprint differs from approval.");
        if (route is null || route.Count == 0) failures.Add("Approval route is missing.");
        else
        {
            // Recheck decision evidence rather than trusting a supplied satisfied flag.
            foreach (var requirement in route.Concat(ApprovalRouteResolver.Build(subject, [], effectiveRules)).DistinctBy(r => (r.Role, r.Department)))
                if (!requirement.IsSatisfied || !ApprovalRouteResolver.IsSatisfied(subject, requirement.Role, requirement.Department, effectiveRules.Fingerprint))
                    failures.Add($"Missing approval: {requirement.Role} {requirement.Department}.".Trim());
        }
        if (preview is null) failures.Add("Posting preview is missing.");
        else
        {
            failures.AddRange(preview.Failures);
            var expected = PostingPreviewBuilder.Build(subject);
            failures.AddRange(expected.Failures);
            if (!preview.Lines.SequenceEqual(expected.Lines)) failures.Add("Posting preview does not match the current transaction.");
            if (!PostingPreviewBuilder.IsBalancedPerFund(preview.Lines)) failures.Add("Posting preview must balance separately per fund and family.");
        }
        return new(failures.Count == 0, failures);
    }

    public static bool ReadyForPaymentHandoff(ValidationSubject subject, DateOnly businessDate) =>
        subject.Transaction.Status == "Posted" && subject.Transaction.Vendor.IsActive
        && !subject.Transaction.PaymentHold && subject.Transaction.DueDate is { } dueDate && dueDate <= businessDate;
}

