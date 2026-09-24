namespace GovErp.Application.Web.Invoices.Commands;

/// <summary>
/// An invoice distribution line on the input of a use case.
/// </summary>
public sealed record DistributionCommand(string Account, decimal Amount, int? PoLineNo);
