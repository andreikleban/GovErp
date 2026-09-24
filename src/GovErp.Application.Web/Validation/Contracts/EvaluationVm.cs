namespace GovErp.Application.Web.Validation.Contracts;

/// <summary>
/// A stored rule evaluation as shown on screen.
/// </summary>
public sealed record EvaluationVm(Guid Id, string TransactionRef, int ContentVersion, Guid ApprovalCycleId, string Trigger, DateTimeOffset EvaluatedAt,
    Guid EvaluatedBy, string EvaluatedByName, string Overall, CapabilitiesVm Capabilities, string EngineVersion, string RuleSetFingerprint,
    IReadOnlyList<AppliedRuleVm> AppliedRules, IReadOnlyList<StepVm> Steps, IReadOnlyList<OutcomeVm> Outcomes,
    IReadOnlyList<RouteStepVm> ApprovalRoute, IReadOnlyList<PreviewLineVm> PostingPreview, PostingCheckVm? PostingCheck,
    bool ReadyForPaymentHandoff, IReadOnlyList<LineBudgetVm> LineBudgets);

/// <summary>
/// One rule outcome as shown on screen.
/// </summary>
public sealed record OutcomeVm(Guid OutcomeRef, string RuleId, int RuleVersion, int Step, string StepName, string Layer, int? Line,
    string Severity, IReadOnlyDictionary<string, string> Inputs, IReadOnlyDictionary<string, string> Computed, string ReasonCode, string Message,
    string Resolution, IReadOnlyList<string> OverridableBy, OverrideInfoVm? OverriddenBy);

/// <summary>
/// Who released a soft stop, and why.
/// </summary>
public sealed record OverrideInfoVm(Guid UserId, string? Role, string Reason, Guid EvaluationId);

/// <summary>
/// Which version of a rule, from which layer, was applied.
/// </summary>
public sealed record AppliedRuleVm(string RuleId, string Layer, int Version, string? ScopeFund, string? ScopeGrant);

/// <summary>
/// A pipeline step on screen: ran or was skipped.
/// </summary>
public sealed record StepVm(int Step, string Name, bool Executed);

/// <summary>
/// One step of the approval route as shown on screen.
/// </summary>
public sealed record RouteStepVm(string Role, string? Department, string Reason, bool IsSatisfied);

/// <summary>
/// One future posting line as shown on screen.
/// </summary>
public sealed record PreviewLineVm(string Account, string Family, decimal Debit, decimal Credit, string Description);

/// <summary>
/// What the evaluation allows the user to do next.
/// </summary>
public sealed record CapabilitiesVm(bool CanSave, bool CanSubmit, bool CanApprove, bool CanPost, bool CanPay);

/// <summary>
/// The posting-readiness check as shown on screen.
/// </summary>
public sealed record PostingCheckVm(bool Passed, IReadOnlyList<string> Failures);
