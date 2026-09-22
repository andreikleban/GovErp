namespace GovErp.Application.Web.Commands;

/// <summary>Запись ap.ProcessedCommands (GE-16).</summary>
public sealed record CommandReceipt(Guid CommandId, UserId ActorId, string CommandType, string RequestHash, string ResultJson, DateTimeOffset At);
