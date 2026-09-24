namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>
/// A row of the budget amendment journal (Budget › Amendments): an amendment of one line with its account.
/// </summary>
public sealed record AmendmentEntryVm(string Account, int FiscalYear, decimal Amount, string Reference, DateOnly EffectiveDate);
