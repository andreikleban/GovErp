namespace GovErp.Application.Web.Validation.Contracts;

/// <summary>
/// Остаток бюджета по счёту строки после этого инвойса — тот же расчёт, что у BUDGET_AVAILABILITY
/// (available + свой резерв − новая потребность по счёту), по входному снимку оценки. Null — строки бюджета нет
/// или снимок строки противоречив.
/// </summary>
public sealed record LineBudgetVm(int LineNo, string Account, int FiscalYear, decimal? AvailableAfter);
