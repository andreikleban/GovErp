namespace GovErp.Web.Components.Shared;

/// <summary>
/// A breadcrumb link: without Href it is text (a module without its own page, or the current page).
/// </summary>
public sealed record Crumb(string Label, string? Href = null);
