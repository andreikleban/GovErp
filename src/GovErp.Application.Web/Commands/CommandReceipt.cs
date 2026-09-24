namespace GovErp.Application.Web.Commands;

/// <summary>
/// A row of ap.ProcessedCommands (GE-16).
/// </summary>
public sealed record CommandReceipt(Guid CommandId, UserId ActorId, string CommandType, string RequestHash, string ResultJson, DateTimeOffset At);
