namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>
/// A released soft stop as shown on screen.
/// </summary>
public sealed record OverrideVm(Guid CycleId, bool IsActive, Guid EvaluationId, Guid OutcomeRef, string RuleId, int RuleVersion, int? Line,
    string Role, Guid UserId, string Reason, DateTimeOffset At);
