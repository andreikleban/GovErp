using GovErp.Application.Web.Validation.Contracts;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Application.Web.Validation;

public static class EvaluationMapping
{
    /// <summary>
    /// evaluatedByName — отображаемое имя актора (Audit, Tenancy.TenantUserNaming); по умолчанию сырой id как заглушка,
    /// как и для инвойса в LastEvaluation, где имя не нужно на каждой загрузке карточки (spec §6, «если дёшево»).
    /// </summary>
    public static EvaluationVm ToVm(EvaluationRecord r, string? evaluatedByName = null) => new(
        r.Id, r.TransactionRef, r.TransactionVersion, r.ApprovalCycleId, r.Trigger.ToString(), r.EvaluatedAt,
        r.EvaluatedBy.Value, evaluatedByName ?? r.EvaluatedBy.Value.ToString(), r.Overall.ToString(),
        new CapabilitiesVm(r.Capabilities.CanSave, r.Capabilities.CanSubmit, r.Capabilities.CanApprove, r.Capabilities.CanPost, r.Capabilities.CanPay),
        r.RuleSetVersions.Engine, r.RuleFingerprint,
        r.RuleSetVersions.AppliedRules.Select(a => new AppliedRuleVm(a.RuleId, a.Layer.ToString(), a.Version, a.ScopeFund, a.ScopeGrant)).ToList(),
        r.Steps.Select(s => new StepVm((int)s.Step, s.Step.ToString(), s.Status == StepExecutionStatus.Executed)).ToList(),
        r.Outcomes.Select(ToVm).ToList(),
        r.ApprovalRoute.Select(a => new RouteStepVm(a.Role.ToString(), a.Department, a.Reason, a.IsSatisfied)).ToList(),
        r.PostingPreview.Select(ToVm).ToList(),
        r.PostingCheck is { } check ? new PostingCheckVm(check.Passed, check.Failures.ToList()) : null,
        r.ReadyForPaymentHandoff,
        LineBudgets(r.InputSnapshot));

    public static OutcomeVm ToVm(RuleOutcome o) => new(
        o.OutcomeRef, o.RuleId, o.RuleVersion, (int)o.Step, o.Step.ToString(), o.Layer.ToString(), o.DistributionLine,
        o.Severity.ToString(), o.Inputs, o.Computed, o.Message, o.Resolution,
        o.OverridableBy.Select(r => r.ToString()).ToList(),
        o.OverriddenBy is { } by ? new OverrideInfoVm(by.UserId.Value, by.Role?.ToString(), by.Reason, by.EvaluationId) : null);

    public static PreviewLineVm ToVm(PostingPreviewLine l) =>
        new(l.Account.ToString(), l.Family, l.Debit.Amount, l.Credit.Amount, l.Description);

    /// <summary>
    /// «Available after» по строкам — формула BUDGET_AVAILABILITY над входным снимком оценки: по счёту и году
    /// available + свой резерв − новая потребность (часть сверх ликвидации PO). Строки одного счёта получают одно значение.
    /// Правило пишет эти цифры только в outcome'ы отказа, поэтому для прошедших строк они вычисляются здесь тем же способом.
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
