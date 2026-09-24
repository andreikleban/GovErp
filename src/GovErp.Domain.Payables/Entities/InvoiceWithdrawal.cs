namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// The author returning an invoice to draft.
/// </summary>
public sealed record InvoiceWithdrawal(UserId UserId, Guid ApprovalCycleId, string Reason, DateTimeOffset At);
