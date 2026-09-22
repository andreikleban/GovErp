using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public static class ApprovalRouteResolver
{
    public const decimal DefaultFinanceDirectorThreshold = 50_000m;

    public static IReadOnlyList<ApprovalRequirement> Build(ValidationSubject subject,
        IReadOnlyList<RuleOutcome> outcomes, EffectiveRuleSet effectiveRules)
    {
        var route = new List<ApprovalRequirement>();
        void Add(ApproverRole role, string? department, string reason)
        {
            if (route.Any(r => r.Role == role && r.Department == department)) return;
            route.Add(new(role, department, reason, IsSatisfied(subject, role, department, effectiveRules.Fingerprint)));
        }

        foreach (var department in subject.Distributions.Select(d => d.Account.Department.Value).Distinct().Order())
            Add(ApproverRole.DepartmentHead, department, $"Department {department} is charged.");
        if (subject.Distributions.Any(d => d.Account.Grant is not null))
            Add(ApproverRole.GrantsManager, null, "Grant-funded distribution.");
        var threshold = effectiveRules.Find("APPROVAL_ROUTE")?.DecimalParameter("finance_director_threshold")
            ?? DefaultFinanceDirectorThreshold;
        if (subject.Transaction.Total >= Money.Of(threshold))
            Add(ApproverRole.FinanceDirector, null, $"Total reaches finance director threshold {Money.Of(threshold)}.");
        foreach (var outcome in outcomes.Where(o => o.Severity == Severity.SoftStop && !o.IsOverridden && o.OverridableBy.Count > 0))
        {
            var role = outcome.OverridableBy[0];
            if (role == ApproverRole.DepartmentHead)
            {
                foreach (var distribution in subject.Distributions.Where(d => outcome.DistributionLine is null || d.LineNo == outcome.DistributionLine))
                    Add(role, distribution.Account.Department.Value, $"Override required for {outcome.RuleId}.");
            }
            else Add(role, null, $"Override required for {outcome.RuleId}.");
        }
        return route.AsReadOnly();
    }

    internal static bool IsSatisfied(ValidationSubject subject, ApproverRole role, string? department, string fingerprint)
    {
        var transaction = subject.Transaction;
        return transaction.ApprovalCycleId != Guid.Empty && !string.IsNullOrWhiteSpace(fingerprint)
            && transaction.RuleFingerprint == fingerprint
            && subject.DetailedApprovals.Any(a => a.IsApproved && a.Role == role
                && (role != ApproverRole.DepartmentHead || department is not null && a.Department?.Value == department)
                && a.UserId.Value != Guid.Empty && a.UserId != transaction.CreatedBy
                && a.ContentVersion == transaction.ContentVersion && a.ApprovalCycleId == transaction.ApprovalCycleId
                && a.RuleFingerprint == fingerprint && a.EvaluationId != Guid.Empty);
    }
}
