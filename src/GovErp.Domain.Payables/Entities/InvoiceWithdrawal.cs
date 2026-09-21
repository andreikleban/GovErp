namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceWithdrawal(UserId UserId, Guid ApprovalCycleId, string Reason, DateTimeOffset At);
