using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Budget.Commands;

public sealed record AmendBudgetCommand(CommandEnvelope Envelope, string Account, int FiscalYear, decimal Amount, string Reference, DateOnly EffectiveDate);
