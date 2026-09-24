namespace GovErp.Application.Web.Invoices;

/// <summary>
/// Sequential numbering of AP documents within a budget year. The counter changes inside the command transaction:
/// a refusal or rollback leaves no gaps, and concurrent creations queue up.
/// </summary>
public interface IInvoiceNumbering
{
    /// <summary>
    /// Prefix of the vendor invoice number that the New form suggests by default.
    /// </summary>
    const string SuggestedPrefix = "INV";

    Task<int> NextAsync(FiscalYear fiscalYear, CancellationToken ct = default);

    /// <summary>
    /// The number the next document will get, without reserving it (a hint for the form).
    /// </summary>
    Task<int> PeekAsync(FiscalYear fiscalYear, CancellationToken ct = default);

    static string Reference(FiscalYear fiscalYear, int sequence) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"AP-{fiscalYear.Year}-{sequence:D6}");

    /// <summary>
    /// A vendor invoice number generated from the same sequence (presets, the form suggestion).
    /// </summary>
    static string GeneratedNumber(string prefix, int sequence) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{prefix}-{sequence:D6}");
}
