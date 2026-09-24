namespace GovErp.Application.Web.Posting;

/// <summary>
/// Journal source of a payment: the invoice reference plus a suffix, so it does not collide with the expenditure entry.
/// </summary>
public static class PaymentSource
{
    public const string Suffix = "/payment";

    public static string For(string invoiceReference) => invoiceReference + Suffix;

    public static string InvoiceReference(string sourceRef) =>
        sourceRef.EndsWith(Suffix, StringComparison.Ordinal) ? sourceRef[..^Suffix.Length] : sourceRef;
}
