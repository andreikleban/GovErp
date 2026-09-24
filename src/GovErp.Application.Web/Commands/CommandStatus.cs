namespace GovErp.Application.Web.Commands;

/// <summary>
/// Outcome of a command: accepted, refused, conflict, forbidden or not found.
/// </summary>
public enum CommandStatus { Accepted, Refused, Conflict, Forbidden, NotFound }
