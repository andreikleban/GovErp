namespace GovErp.Domain.Validation.Exceptions;

/// <summary>
/// The rule configuration cannot be used. Carries a code from ValidationErrors; the text is rendered by the application.
/// </summary>
public sealed class ValidationException(string code, params (string Name, object? Value)[] args) : Exception(Problem.Of(code, args).ToString())
{
    public Problem Problem { get; } = Problem.Of(code, args);
}
