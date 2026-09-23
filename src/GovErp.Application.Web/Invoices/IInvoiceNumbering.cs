namespace GovErp.Application.Web.Invoices;

/// <summary>
/// Сквозная нумерация AP-документов внутри бюджетного года. Счётчик меняется в транзакции команды:
/// отказ или откат не оставляет пропусков, параллельные создания выстраиваются в очередь.
/// </summary>
public interface IInvoiceNumbering
{
    /// <summary>Префикс номера поставщика, который форма New предлагает по умолчанию.</summary>
    const string SuggestedPrefix = "INV";

    Task<int> NextAsync(FiscalYear fiscalYear, CancellationToken ct = default);

    /// <summary>Номер, который получит следующий документ, без резервирования (для подсказки в форме).</summary>
    Task<int> PeekAsync(FiscalYear fiscalYear, CancellationToken ct = default);

    static string Reference(FiscalYear fiscalYear, int sequence) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"AP-{fiscalYear.Year}-{sequence:D6}");

    /// <summary>Номер поставщика, сгенерированный из той же последовательности (пресеты, предложение в форме).</summary>
    static string GeneratedNumber(string prefix, int sequence) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{prefix}-{sequence:D6}");
}
