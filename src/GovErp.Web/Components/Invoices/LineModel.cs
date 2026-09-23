using GovErp.Application.Web.Invoices.Contracts;

namespace GovErp.Web.Components.Invoices;

/// <summary>Строка формы: сегменты счёта по отдельности, сумма и строка PO.</summary>
public sealed class LineModel
{
    public string Fund { get; set; } = "";
    public string Department { get; set; } = "";
    public string Object { get; set; } = "";
    public string? Grant { get; set; }
    public decimal Amount { get; set; }
    public int? PoLineNo { get; set; }

    public string Account => string.IsNullOrWhiteSpace(Grant) ? $"{Fund}-{Department}-{Object}" : $"{Fund}-{Department}-{Object}-{Grant}";

    /// <summary>Выпадающие списки сегментов не имеют пустого значения, поэтому новая строка сразу получает валидные коды.</summary>
    public static LineModel Default() => new() { Fund = "101", Department = "6000", Object = "53100" };

    public static LineModel From(DistributionVm d)
    {
        var line = new LineModel { Amount = d.Amount, PoLineNo = d.PoLineNo };
        line.SetAccount(d.Account);
        return line;
    }

    /// <summary>Разбирает код счёта «фонд-отдел-объект[-грант]»; грант может сам содержать дефисы.</summary>
    public void SetAccount(string account)
    {
        var parts = account.Split('-');
        Fund = parts.ElementAtOrDefault(0) ?? "";
        Department = parts.ElementAtOrDefault(1) ?? "";
        Object = parts.ElementAtOrDefault(2) ?? "";
        Grant = parts.Length > 3 ? string.Join('-', parts.Skip(3)) : null;
    }
}
