using GovErp.Application.Web.Validation.Contracts;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Application.Web.Validation;

public static class EvaluationMapping
{
    public static EvaluationVm ToVm(EvaluationRecord r) => new(
        r.Id, r.TransactionRef, r.TransactionVersion, r.ApprovalCycleId, r.Trigger.ToString(), r.EvaluatedAt,
        r.EvaluatedBy.Value, r.Overall.ToString(),
        new CapabilitiesVm(r.Capabilities.CanSave, r.Capabilities.CanSubmit, r.Capabilities.CanApprove, r.Capabilities.CanPost, r.Capabilities.CanPay),
        r.RuleSetVersions.Engine, r.RuleFingerprint,
        r.RuleSetVersions.AppliedRules.Select(a => new AppliedRuleVm(a.RuleId, a.Layer.ToString(), a.Version, a.ScopeFund, a.ScopeGrant)).ToList(),
        r.Steps.Select(s => new StepVm((int)s.Step, s.Step.ToString(), s.Status == StepExecutionStatus.Executed)).ToList(),
        r.Outcomes.Select(ToVm).ToList(),
        r.ApprovalRoute.Select(a => new RouteStepVm(a.Role.ToString(), a.Department, a.Reason, a.IsSatisfied)).ToList(),
        r.PostingPreview.Select(ToVm).ToList(),
        r.PostingCheck is { } check ? new PostingCheckVm(check.Passed, check.Failures.ToList()) : null,
        r.ReadyForPaymentHandoff);

    public static OutcomeVm ToVm(RuleOutcome o) => new(
        o.OutcomeRef, o.RuleId, o.RuleVersion, (int)o.Step, o.Step.ToString(), o.Layer.ToString(), o.DistributionLine,
        o.Severity.ToString(), o.Inputs, o.Computed, o.Message, o.Resolution,
        o.OverridableBy.Select(r => r.ToString()).ToList(),
        o.OverriddenBy is { } by ? new OverrideInfoVm(by.UserId.Value, by.Role?.ToString(), by.Reason, by.EvaluationId) : null);

    public static PreviewLineVm ToVm(PostingPreviewLine l) =>
        new(l.Account.ToString(), l.Family, l.Debit.Amount, l.Credit.Amount, l.Description);
}
