namespace GovErp.Web.Components.Layout;

/// <summary>
/// Модули в порядке потока данных: настройка → бюджет → закупки → кредиторка → главная книга → аудит.
/// Пункты появляются по мере готовности экранов; пустой модуль в меню не выводится.
/// </summary>
public static class ModuleMenu
{
    public static readonly IReadOnlyList<MenuModule> Modules =
    [
        new("Setup", "Validation rules and reference data.", [new MenuEntry("Rules", "/rules")]),
        new("Budget", "Budget lines, amendments and what holds the budget.", [new MenuEntry("Budget Lines", "/budget")]),
        new("Purchasing", "Purchase orders and their encumbrances.", []),
        new("Payables", "Vendor invoices and the approval queue.",
            [new MenuEntry("Invoices", "/invoices"), new MenuEntry("Approvals", "/approvals")]),
        new("General Ledger", "Journal and fiscal periods.", []),
        new("Audit", "Evaluations and audit events.", []),
    ];

    public static IEnumerable<MenuModule> Visible => Modules.Where(m => m.Entries.Count > 0);
}
