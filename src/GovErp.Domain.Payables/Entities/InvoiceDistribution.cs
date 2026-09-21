namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceDistribution(int LineNo, AccountCode Account, Money Amount, int? PoLineNo);
