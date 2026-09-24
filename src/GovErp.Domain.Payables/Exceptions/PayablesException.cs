namespace GovErp.Domain.Payables.Exceptions;

/// <summary>
/// A payables rule was broken. Carries a code from PayablesErrors and its arguments; the text is rendered by the application.
/// </summary>
public sealed class PayablesException(string code, params (string Name, object? Value)[] args) : Exception(Problem.Of(code, args).ToString())
{
    public Problem Problem { get; } = Problem.Of(code, args);
}
