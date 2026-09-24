using GovErp.Application.Web.Common;
using GovErp.Application.Web.Validation.Contracts;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Application.Web.Validation;

/// <summary>
/// Builds an evaluation view: rule codes become catalog text.
/// </summary>
public static class EvaluationMapping
{
    /// <summary>
    /// evaluatedByName is the actor's display name (Audit, Tenancy.TenantUserNaming); by default the raw id as a fallback,
    /// as for the invoice's LastEvaluation, where the name is not needed on every card load (spec §6, "if cheap").
    /// </summary>
    public static EvaluationVm ToVm(EvaluationRecord r, string? evaluatedByName = null) => new(
        r.Id, r.TransactionRef, r.TransactionVersion, r.ApprovalCycleId, r.Trigger.ToString(), r.EvaluatedAt,
        r.EvaluatedBy.Value, evaluatedByName ?? r.EvaluatedBy.Value.ToString(), r.Overall.ToString(),
        new CapabilitiesVm(r.Capabilities.CanSave, r.Capabilities.CanSubmit, r.Capabilities.CanApprove, r.Capabilities.CanPost, r.Capabilities.CanPay),
        r.RuleSetVersions.Engine, r.RuleFingerprint,
        r.RuleSetVersions.AppliedRules.Select(a => new AppliedRuleVm(a.RuleId, a.Layer.ToString(), a.Version, a.ScopeFund, a.ScopeGrant)).ToList(),
        r.Steps.Select(s => new StepVm((int)s.Step, s.Step.ToString(), s.Status == StepExecutionStatus.Executed)).ToList(),
        r.Outcomes.Select(ToVm).ToList(),
        r.ApprovalRoute.Select(a => new RouteStepVm(a.Role.ToString(), a.Department, Messages.Render(a.Reason), a.IsSatisfied)).ToList(),
        r.PostingPreview.Select(ToVm).ToList(),
        r.PostingCheck is { } check ? new PostingCheckVm(check.Passed, check.Failures.Select(Messages.Render).ToList()) : null,
        r.ReadyForPaymentHandoff,
        LineBudgets(r.InputSnapshot));

    public static OutcomeVm ToVm(RuleOutcome o) => new(
        o.OutcomeRef, o.RuleId, o.RuleVersion, (int)o.Step, o.Step.ToString(), o.Layer.ToString(), o.DistributionLine,
        o.Severity.ToString(), o.Inputs, o.Computed, o.ReasonCode, Messages.Render(o), o.Resolution,
        o.OverridableBy.Select(r => r.ToString()).ToList(),
        o.OverriddenBy is { } by ? new OverrideInfoVm(by.UserId.Value, by.Role?.ToString(), by.Reason, by.EvaluationId) : null);

    public static PreviewLineVm ToVm(PostingPreviewLine l) =>
        new(l.Account.ToString(), l.Family, l.Debit.Amount, l.Credit.Amount, l.Description);

    /// <summary>
    /// "Available after" per line is the BUDGET_AVAILABILITY formula over the evaluation's input snapshot: per account and year,
    /// available + own reservation − new need (the part above the PO liquidation). Lines of the same account get the same value.
    /// The rule writes these figures only into failing outcomes, so for passed lines they are computed here the same way.
    /// </summary>
    public static IReadOnlyList<LineBudgetVm> LineBudgets(ValidationSubject? subject)
    {
        if (subject?.Distributions is not { Count: > 0 })
        {
            return [];
        }

        var result = new List<LineBudgetVm>();
        foreach (var group in BudgetAllocation.Allocate(subject).GroupBy(x => (x.Distribution.Account, x.Distribution.Budget.FiscalYear)))
        {
            var budget = group.First().Distribution.Budget;
            decimal? after = budget.Exists && group.All(x => x.Error is null)
                ? budget.AvailableForThisInvoice.Amount - group.Sum(x => x.RequiredNewBudget.Amount)
                : null;
            result.AddRange(group.Select(x => new LineBudgetVm(x.Distribution.LineNo, x.Distribution.Account.ToString(), budget.FiscalYear, after)));
        }

        return result.OrderBy(l => l.LineNo).ToList();
    }
}
