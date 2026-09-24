using System.Globalization;

namespace GovErp.Web.Formatting;

/// <summary>
/// Formats amounts in the UI.
/// </summary>
public static class MoneyText
{
    public static string Usd(decimal amount) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} USD");
}
