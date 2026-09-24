using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Commands;

/// <summary>
/// Code is the problem code of a refusal (for callers and tests); Reason is its text for people, rendered from Messages.
/// </summary>
public sealed record CommandResult<T>(CommandStatus Status, T? Value, string? Reason, bool Retryable, string? Code = null)
{
    public bool IsAccepted => Status == CommandStatus.Accepted;

    public static CommandResult<T> Accepted(T value) => new(CommandStatus.Accepted, value, null, false);

    /// <summary>
    /// Business refusal: the command did not change financial state; Value carries the current evaluation.
    /// </summary>
    public static CommandResult<T> Refused(T? value, Problem problem) => new(CommandStatus.Refused, value, Messages.Render(problem), false, problem.Code);

    public static CommandResult<T> Refused(T? value, string code, params (string Name, object? Value)[] args) => Refused(value, Problem.Of(code, args));

    public static CommandResult<T> Conflict(Problem problem) => new(CommandStatus.Conflict, default, Messages.Render(problem), true, problem.Code);

    public static CommandResult<T> Forbidden(Problem problem) => new(CommandStatus.Forbidden, default, Messages.Render(problem), false, problem.Code);

    public static CommandResult<T> Forbidden(string code, params (string Name, object? Value)[] args) => Forbidden(Problem.Of(code, args));

    public static CommandResult<T> NotFound(Problem problem) => new(CommandStatus.NotFound, default, Messages.Render(problem), false, problem.Code);

    public static CommandResult<T> NotFound(string code, params (string Name, object? Value)[] args) => NotFound(Problem.Of(code, args));
}
