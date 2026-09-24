using GovErp.Domain.Validation.Codes;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Step 8: whether the document can be posted given the current evaluation.
/// </summary>
public static class PostingEligibility
{
    public static PostingCheck Check(ValidationSubject subject, Severity? overall,
        IReadOnlyList<ApprovalRequirement>? route, PostingPreview? preview, EffectiveRuleSet effectiveRules)
    {
        var failures = new List<Problem>();
        if (subject.Transaction.Status != "Approved") failures.Add(Problem.Of(PostingErrors.NotApproved, ("status", subject.Transaction.Status)));
        if (overall is not (Severity.Allowed or Severity.Warning)) failures.Add(Problem.Of(PostingErrors.OpenStops));
        if (!subject.PeriodIsOpen) failures.Add(Problem.Of(PostingErrors.PeriodClosed));
        if (string.IsNullOrWhiteSpace(effectiveRules.Fingerprint) || subject.Transaction.RuleFingerprint != effectiveRules.Fingerprint
            || subject.VersionsAtLastApproval is { Fingerprint.Length: > 0 } approved && approved.Fingerprint != effectiveRules.Fingerprint)
            failures.Add(Problem.Of(PostingErrors.RevalidationRequired));
        if (route is null || route.Count == 0) failures.Add(Problem.Of(PostingErrors.RouteMissing));
        else
        {
            // Recheck decision evidence rather than trusting a supplied satisfied flag.
            foreach (var requirement in route.Concat(ApprovalRouteResolver.Build(subject, [], effectiveRules)).DistinctBy(r => (r.Role, r.Department)))
                if (!requirement.IsSatisfied || !ApprovalRouteResolver.IsSatisfied(subject, requirement.Role, requirement.Department, effectiveRules.Fingerprint))
                    failures.Add(Problem.Of(PostingErrors.ApprovalMissing, ("role", requirement.Role), ("department", requirement.Department)));
        }
        if (preview is null) failures.Add(Problem.Of(PostingErrors.PreviewMissing));
        else
        {
            failures.AddRange(preview.Failures);
            var expected = PostingPreviewBuilder.Build(subject);
            failures.AddRange(expected.Failures);
            if (!preview.Lines.SequenceEqual(expected.Lines)) failures.Add(Problem.Of(PostingErrors.PreviewMismatch));
            if (!PostingPreviewBuilder.IsBalancedPerFund(preview.Lines)) failures.Add(Problem.Of(PostingErrors.PreviewUnbalanced));
        }
        return new(failures.Count == 0, failures);
    }

    public static bool ReadyForPaymentHandoff(ValidationSubject subject, DateOnly businessDate) =>
        subject.Transaction.Status == "Posted" && subject.Transaction.Vendor.IsActive
        && !subject.Transaction.PaymentHold && subject.Transaction.DueDate is { } dueDate && dueDate <= businessDate;
}

