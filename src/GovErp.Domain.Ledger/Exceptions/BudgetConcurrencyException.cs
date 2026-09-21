namespace GovErp.Domain.Ledger.Exceptions;

/// <summary>Бросается реализацией репозитория при конфликте rowversion. Слой сценариев ловит без ссылки на EF.</summary>
public sealed class BudgetConcurrencyException(AccountCode account, FiscalYear fiscalYear)
    : LedgerException($"Budget line {account} {fiscalYear} was modified concurrently.")
{
    public AccountCode Account { get; } = account;
    public FiscalYear FiscalYear { get; } = fiscalYear;
}
