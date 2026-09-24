namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>
/// An invoice line as shown on screen.
/// </summary>
public sealed record DistributionVm(int LineNo, string Account, decimal Amount, int? PoLineNo);
