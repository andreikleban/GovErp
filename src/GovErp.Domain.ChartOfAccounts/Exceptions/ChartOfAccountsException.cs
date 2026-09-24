namespace GovErp.Domain.ChartOfAccounts.Exceptions;

/// <summary>
/// A chart-of-accounts rule was broken. Carries a code from ChartOfAccountsErrors; the text is rendered by the application.
/// </summary>
public sealed class ChartOfAccountsException(string code, params (string Name, object? Value)[] args) : Exception(Problem.Of(code, args).ToString())
{
    public Problem Problem { get; } = Problem.Of(code, args);
}
