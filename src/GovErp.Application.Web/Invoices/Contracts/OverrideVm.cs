namespace GovErp.Application.Web.Invoices.Contracts;

public sealed record OverrideVm(Guid CycleId, bool IsActive, Guid EvaluationId, Guid OutcomeRef, string RuleId, int RuleVersion, int? Line,
    string Role, Guid UserId, string Reason, DateTimeOffset At);
