namespace GovErp.Application.Web.Budget.Contracts;

public sealed record BudgetLineVm(string Account, int FiscalYear, string ControlMode, decimal OpeningActuals, decimal OpeningEncumbered,
    decimal Adopted, decimal Amended, decimal Actuals, decimal Encumbered, decimal Held, decimal Available, IReadOnlyList<AmendmentVm> Amendments);

public sealed record AmendmentVm(decimal Amount, string Reference, DateOnly EffectiveDate);
