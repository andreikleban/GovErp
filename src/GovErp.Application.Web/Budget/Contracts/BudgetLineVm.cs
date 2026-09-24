namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>
/// A budget line on screen: balances, control mode and amendments.
/// </summary>
public sealed record BudgetLineVm(string Account, int FiscalYear, string ControlMode, decimal OpeningActuals, decimal OpeningEncumbered,
    decimal Adopted, decimal Amended, decimal Actuals, decimal Encumbered, decimal Held, decimal Available, IReadOnlyList<AmendmentVm> Amendments);

/// <summary>
/// One amendment of the adopted budget.
/// </summary>
public sealed record AmendmentVm(decimal Amount, string Reference, DateOnly EffectiveDate);
