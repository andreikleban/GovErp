namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceOverride(OverrideTarget Target, UserId UserId, string Reason, DateTimeOffset At);
