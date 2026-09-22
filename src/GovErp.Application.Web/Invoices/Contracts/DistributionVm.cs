namespace GovErp.Application.Web.Invoices.Contracts;

public sealed record DistributionVm(int LineNo, string Account, decimal Amount, int? PoLineNo);
