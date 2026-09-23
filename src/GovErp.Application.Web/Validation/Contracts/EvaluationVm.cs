namespace GovErp.Application.Web.Validation.Contracts;

public sealed record EvaluationVm(Guid Id, string TransactionRef, int ContentVersion, Guid ApprovalCycleId, string Trigger, DateTimeOffset EvaluatedAt,
    Guid EvaluatedBy, string Overall, CapabilitiesVm Capabilities, string EngineVersion, string RuleSetFingerprint,
    IReadOnlyList<AppliedRuleVm> AppliedRules, IReadOnlyList<StepVm> Steps, IReadOnlyList<OutcomeVm> Outcomes,
    IReadOnlyList<RouteStepVm> ApprovalRoute, IReadOnlyList<PreviewLineVm> PostingPreview, PostingCheckVm? PostingCheck,
    bool ReadyForPaymentHandoff, IReadOnlyList<LineBudgetVm> LineBudgets);

public sealed record OutcomeVm(Guid OutcomeRef, string RuleId, int RuleVersion, int Step, string StepName, string Layer, int? Line,
    string Severity, IReadOnlyDictionary<string, string> Inputs, IReadOnlyDictionary<string, string> Computed, string Message,
    string Resolution, IReadOnlyList<string> OverridableBy, OverrideInfoVm? OverriddenBy);

public sealed record OverrideInfoVm(Guid UserId, string? Role, string Reason, Guid EvaluationId);

public sealed record AppliedRuleVm(string RuleId, string Layer, int Version, string? ScopeFund, string? ScopeGrant);

public sealed record StepVm(int Step, string Name, bool Executed);

public sealed record RouteStepVm(string Role, string? Department, string Reason, bool IsSatisfied);

public sealed record PreviewLineVm(string Account, string Family, decimal Debit, decimal Credit, string Description);

public sealed record CapabilitiesVm(bool CanSave, bool CanSubmit, bool CanApprove, bool CanPost, bool CanPay);

public sealed record PostingCheckVm(bool Passed, IReadOnlyList<string> Failures);
