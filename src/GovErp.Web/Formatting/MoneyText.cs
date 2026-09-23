using System.Globalization;

namespace GovErp.Web.Formatting;

public static class MoneyText
{
    public static string Usd(decimal amount) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} USD");
}
