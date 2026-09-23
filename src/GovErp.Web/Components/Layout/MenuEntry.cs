using Microsoft.AspNetCore.Components.Routing;

namespace GovErp.Web.Components.Layout;

/// <summary>Пункт меню модуля: экран-список; Prefix подсвечивает пункт и на карточках под этим адресом.</summary>
public sealed record MenuEntry(string Label, string Href, NavLinkMatch Match = NavLinkMatch.Prefix);
