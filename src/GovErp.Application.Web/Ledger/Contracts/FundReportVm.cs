namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>
/// One fund on the business-date fiscal year: budget availability from the budget lines, and the financial journal totals.
/// </summary>
public sealed record FundReportVm(
    string Code,
    string Name,
    string Type,
    string Basis,
    int FiscalYear,
    decimal Amended,
    decimal Actuals,
    decimal Encumbered,
    decimal Held,
    decimal Available,
    decimal FinancialDebit,
    decimal FinancialCredit)
{
    public bool FinancialBalanced => FinancialDebit == FinancialCredit;
}
