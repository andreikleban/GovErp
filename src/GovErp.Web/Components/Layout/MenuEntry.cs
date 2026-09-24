using Microsoft.AspNetCore.Components.Routing;

namespace GovErp.Web.Components.Layout;

/// <summary>A module menu item: a list screen; Prefix also highlights the item on the cards under that address.</summary>
public sealed record MenuEntry(string Label, string Href, NavLinkMatch Match = NavLinkMatch.Prefix);
