namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// A Soft Stop release: what was released (Target), by whom, in which role and why. Later evaluations need the role to check authority.
/// </summary>
public sealed record InvoiceOverride(OverrideTarget Target, ApproverRole Role, UserId UserId, string Reason, DateTimeOffset At);
