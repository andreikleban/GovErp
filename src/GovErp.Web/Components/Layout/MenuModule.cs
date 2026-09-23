namespace GovErp.Web.Components.Layout;

/// <summary>Модуль левого меню. Модуль без готовых экранов не показывается, но место в порядке меню за ним сохранено.</summary>
public sealed record MenuModule(string Name, string Summary, IReadOnlyList<MenuEntry> Entries);
