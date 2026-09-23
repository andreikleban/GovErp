namespace GovErp.Web.Components.Shared;

/// <summary>Звено хлебных крошек: без Href — текст (модуль без своей страницы или текущая страница).</summary>
public sealed record Crumb(string Label, string? Href = null);
