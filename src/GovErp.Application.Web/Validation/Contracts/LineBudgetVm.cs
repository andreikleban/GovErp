namespace GovErp.Application.Web.Validation.Contracts;

/// <summary>
/// Budget remaining on the line's account after this invoice, the same calculation as BUDGET_AVAILABILITY
/// (available + own reservation − new need on the account), from the evaluation's input snapshot. Null when there is no budget line
/// or the line snapshot is inconsistent.
/// </summary>
public sealed record LineBudgetVm(int LineNo, string Account, int FiscalYear, decimal? AvailableAfter);
