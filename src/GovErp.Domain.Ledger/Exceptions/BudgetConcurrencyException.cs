namespace GovErp.Domain.Ledger.Exceptions;

/// <summary>Thrown by the repository implementation on a rowversion conflict. The use-case layer catches it without referencing EF.</summary>
public sealed class BudgetConcurrencyException(AccountCode account, FiscalYear fiscalYear)
    : LedgerException($"Budget line {account} {fiscalYear} was modified concurrently.")
{
    public AccountCode Account { get; } = account;
    public FiscalYear FiscalYear { get; } = fiscalYear;
}
