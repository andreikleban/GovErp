namespace GovErp.Application.Web.Common;

/// <summary>
/// The actor may not do this. Carries a code from AppErrors; the runner turns it into Forbidden.
/// </summary>
public sealed class AuthorizationException(string code, params (string Name, object? Value)[] args) : Exception(Problem.Of(code, args).ToString())
{
    public Problem Problem { get; } = Problem.Of(code, args);
}
