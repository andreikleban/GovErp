namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>
/// A journal line as shown on screen.
/// </summary>
public sealed record JournalLineVm(string Account, string Family, decimal Debit, decimal Credit, string Description);
