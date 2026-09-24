namespace GovErp.Application.Web.Audit.Contracts;

/// <summary>Audit event list filters; null means no restriction. All three are case-insensitive substrings
/// (Action, the stored event's ActorName and SubjectRef).</summary>
public sealed record EventFilter(string? Action = null, string? Actor = null, string? Subject = null)
{
    public static readonly EventFilter None = new();
}
