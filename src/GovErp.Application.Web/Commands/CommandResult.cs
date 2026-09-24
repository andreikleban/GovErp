namespace GovErp.Application.Web.Commands;

public sealed record CommandResult<T>(CommandStatus Status, T? Value, string? Reason, bool Retryable)
{
    public bool IsAccepted => Status == CommandStatus.Accepted;

    public static CommandResult<T> Accepted(T value) => new(CommandStatus.Accepted, value, null, false);

    /// <summary>Business refusal: the command did not change financial state; Value carries the current evaluation.</summary>
    public static CommandResult<T> Refused(T? value, string reason) => new(CommandStatus.Refused, value, reason, false);

    public static CommandResult<T> Conflict(string reason) => new(CommandStatus.Conflict, default, reason, true);

    public static CommandResult<T> Forbidden(string reason) => new(CommandStatus.Forbidden, default, reason, false);

    public static CommandResult<T> NotFound(string reason) => new(CommandStatus.NotFound, default, reason, false);
}
