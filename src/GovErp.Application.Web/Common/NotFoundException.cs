namespace GovErp.Application.Web.Common;

/// <summary>
/// The requested item does not exist. Carries a code from AppErrors; the runner turns it into NotFound.
/// </summary>
public sealed class NotFoundException(string code, params (string Name, object? Value)[] args) : Exception(Problem.Of(code, args).ToString())
{
    public Problem Problem { get; } = Problem.Of(code, args);

    public string Code => Problem.Code;

    public string Reason => Messages.Render(Problem);
}
