namespace GovErp.Application.Web.Invoices.Commands;

public sealed record DistributionCommand(string Account, decimal Amount, int? PoLineNo);
