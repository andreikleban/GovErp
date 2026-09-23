namespace GovErp.Application.Web.Audit.Contracts;

/// <summary>Фильтры списка событий аудита; null — без ограничения. Все три — подстрока без учёта регистра
/// (Action, ActorName сохранённого события и SubjectRef).</summary>
public sealed record EventFilter(string? Action = null, string? Actor = null, string? Subject = null)
{
    public static readonly EventFilter None = new();
}
