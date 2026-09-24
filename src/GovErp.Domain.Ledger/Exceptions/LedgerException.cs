namespace GovErp.Domain.Ledger.Exceptions;

/// <summary>
/// A ledger rule was broken. Carries a code from LedgerErrors and its arguments; the text is rendered by the application.
/// </summary>
public class LedgerException(string code, params (string Name, object? Value)[] args) : Exception(Problem.Of(code, args).ToString())
{
    public Problem Problem { get; } = Problem.Of(code, args);
}
