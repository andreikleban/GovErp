namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// Invoice status: draft, then submitted, then approved or rejected, then posted.
/// </summary>
public enum InvoiceStatus { Draft, Submitted, Approved, Rejected, Posted }
