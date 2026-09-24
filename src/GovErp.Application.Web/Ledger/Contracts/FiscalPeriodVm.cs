namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>
/// A fiscal period as shown on screen.
/// </summary>
public sealed record FiscalPeriodVm(int Year, int Month, string Status);
