using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Budget.Commands;

/// <summary>
/// Command to amend the adopted budget of one account and fiscal year.
/// </summary>
public sealed record AmendBudgetCommand(CommandEnvelope Envelope, string Account, int FiscalYear, decimal Amount, string Reference, DateOnly EffectiveDate);
