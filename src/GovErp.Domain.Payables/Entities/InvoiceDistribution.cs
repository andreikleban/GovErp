namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// One distribution line of an invoice.
/// </summary>
public sealed record InvoiceDistribution(int LineNo, AccountCode Account, Money Amount, int? PoLineNo);
