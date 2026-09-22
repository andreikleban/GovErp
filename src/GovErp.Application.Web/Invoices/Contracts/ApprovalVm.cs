namespace GovErp.Application.Web.Invoices.Contracts;

public sealed record ApprovalVm(Guid CycleId, bool IsActiveCycle, int ContentVersion, string Role, string? Department, Guid UserId,
    string Decision, string? Reason, DateTimeOffset At);
