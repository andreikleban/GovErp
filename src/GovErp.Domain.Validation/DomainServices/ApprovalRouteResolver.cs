using GovErp.Domain.Validation.Codes;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Builds the approval route from the snapshot and the rules in force.
/// </summary>
public static class ApprovalRouteResolver
{
    public const decimal DefaultFinanceDirectorThreshold = 50_000m;

    private static readonly ParameterSpec FinanceDirectorThreshold =
        ParameterSpec.Amount("finance_director_threshold", Stricter.WhenLower);

    /// <summary>
    /// Parameters of the APPROVAL_ROUTE definition.
    /// </summary>
    public static IReadOnlyList<ParameterSpec> Parameters { get; } = [FinanceDirectorThreshold];

    public static IReadOnlyList<ApprovalRequirement> Build(ValidationSubject subject,
        IReadOnlyList<RuleOutcome> outcomes, EffectiveRuleSet effectiveRules)
    {
        var route = new List<ApprovalRequirement>();
        void Add(ApproverRole role, string? department, Problem reason)
        {
            if (route.Any(r => r.Role == role && r.Department == department)) return;
            route.Add(new(role, department, reason, IsSatisfied(subject, role, department, effectiveRules.Fingerprint)));
        }

        foreach (var department in subject.Distributions.Select(d => d.Account.Department.Value).Distinct().Order())
            Add(ApproverRole.DepartmentHead, department, Problem.Of(RouteReasons.DepartmentCharged, ("department", department)));
        if (subject.Distributions.Any(d => d.Account.Grant is not null))
            Add(ApproverRole.GrantsManager, null, Problem.Of(RouteReasons.GrantFunded));
        // With scoped rules, the strictest (lowest) threshold among the invoice's funds and grants applies.
        var threshold = subject.Distributions
            .Select(d => effectiveRules.ForScope(d.Account.Fund.Value, d.Account.Grant?.Value).Find(RuleCatalog.ApprovalRouteRuleId))
            .Select(rule => rule?.DecimalParameter(FinanceDirectorThreshold.Name) ?? DefaultFinanceDirectorThreshold)
            .DefaultIfEmpty(DefaultFinanceDirectorThreshold)
            .Min();
        if (subject.Transaction.Total >= Money.Of(threshold))
            Add(ApproverRole.FinanceDirector, null, Problem.Of(RouteReasons.FinanceDirectorThreshold, ("threshold", Money.Of(threshold)), ("total", subject.Transaction.Total)));
        foreach (var outcome in outcomes.Where(o => o.Severity == Severity.SoftStop && !o.IsOverridden && o.OverridableBy.Count > 0))
        {
            var role = outcome.OverridableBy[0];
            if (role == ApproverRole.DepartmentHead)
            {
                foreach (var distribution in subject.Distributions.Where(d => outcome.DistributionLine is null || d.LineNo == outcome.DistributionLine))
                    Add(role, distribution.Account.Department.Value, Problem.Of(RouteReasons.OverrideRequired, ("rule", outcome.RuleId)));
            }
            else Add(role, null, Problem.Of(RouteReasons.OverrideRequired, ("rule", outcome.RuleId)));
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
