namespace GovErp.Web.Components.Layout;

/// <summary>
/// Модули в порядке потока данных: настройка → бюджет → закупки → кредиторка → главная книга → аудит.
/// Пункты появляются по мере готовности экранов; пустой модуль в меню не выводится.
/// </summary>
public static class ModuleMenu
{
    public static readonly IReadOnlyList<MenuModule> Modules =
    [
        new("Setup", "Validation rules and reference data.",
            [new MenuEntry("Funds", "/funds"), new MenuEntry("Departments", "/departments"), new MenuEntry("Objects", "/objects"),
             new MenuEntry("Grants", "/grants"), new MenuEntry("Account Combinations", "/combinations"), new MenuEntry("Vendors", "/vendors"),
             new MenuEntry("Rules", "/rules"), new MenuEntry("Users", "/users"), new MenuEntry("Roles", "/roles")]),
        new("Budget", "Budget lines, amendments and what holds the budget.",
            // Match.All на "Budget Lines": иначе он подсвечивался бы и на Amendments/Encumbrances (общий префикс "/budget").
            [new MenuEntry("Budget Lines", "/budget", Microsoft.AspNetCore.Components.Routing.NavLinkMatch.All),
             new MenuEntry("Amendments", "/budget/amendments"), new MenuEntry("Encumbrances", "/budget/encumbrances")]),
        new("Purchasing", "Purchase orders and their encumbrances.", [new MenuEntry("Purchase Orders", "/purchasing/orders")]),
        new("Payables", "Vendor invoices and the approval queue.",
            [new MenuEntry("Invoices", "/invoices"), new MenuEntry("Approvals", "/approvals")]),
        new("General Ledger", "Journal and fiscal periods.",
            [new MenuEntry("Journal", "/ledger/journal"), new MenuEntry("Periods", "/ledger/periods")]),
        new("Audit", "Evaluations and audit events.",
            [new MenuEntry("Evaluations", "/audit/evaluations"), new MenuEntry("Events", "/audit/events")]),
    ];

    public static IEnumerable<MenuModule> Visible => Modules.Where(m => m.Entries.Count > 0);
}
