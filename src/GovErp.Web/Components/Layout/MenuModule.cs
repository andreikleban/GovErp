namespace GovErp.Web.Components.Layout;

/// <summary>
/// A left menu module. A module without ready screens is not shown, but its place in the menu order is kept.
/// </summary>
public sealed record MenuModule(string Name, string Summary, IReadOnlyList<MenuEntry> Entries);
