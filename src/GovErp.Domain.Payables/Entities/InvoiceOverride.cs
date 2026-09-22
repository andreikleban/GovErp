namespace GovErp.Domain.Payables.Entities;

/// <summary>Снятие Soft Stop: что снято (Target), кем и в какой роли, почему. Роль нужна последующим оценкам для проверки полномочий.</summary>
public sealed record InvoiceOverride(OverrideTarget Target, ApproverRole Role, UserId UserId, string Reason, DateTimeOffset At);
