using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices.Commands;

namespace GovErp.Application.Web.Invoices;

/// <summary>
/// Demo invoices of spec §2.3. The number is unique and idempotent: the same CommandId yields the same number.
/// The vendor id comes as a parameter: Application does not reference Infrastructure seed data.
/// </summary>
public static class Presets
{
    public const string VendorCode = "ACME";

    private static readonly DateOnly DocumentDate = new(2026, 6, 15);
    private static readonly DateOnly DueDate = new(2026, 7, 15);

    public static CreateInvoiceCommand For(InvoicePreset preset, CommandEnvelope envelope, Guid vendorId) => preset switch
    {
        InvoicePreset.NonPoGrant => Command(envelope, vendorId, "NPO", 160_000m, null,
            new DistributionCommand("701-6000-53100-G-COPS-26", 160_000m, null)),
        InvoicePreset.PoBackedGrant => Command(envelope, vendorId, "PO", 160_000m, "PO-2026-0451",
            new DistributionCommand("701-3000-53100-G-COPS-26", 160_000m, 1)),
        InvoicePreset.MultiFund => Command(envelope, vendorId, "MF", 30_000m, null,
            new DistributionCommand("101-6000-53100", 12_000m, null),
            new DistributionCommand("202-4000-53100", 8_000m, null),
            new DistributionCommand("501-5000-53100", 10_000m, null)),
        _ => throw new InvalidValueException(nameof(preset), AppErrors.UnknownInvoicePreset, ("preset", preset)),
    };

    private static CreateInvoiceCommand Command(CommandEnvelope envelope, Guid vendorId, string prefix, decimal total, string? poRef,
        params DistributionCommand[] lines) =>
        new(envelope, "", vendorId, DocumentDate, DocumentDate, DocumentDate, DueDate, total, poRef, lines, GeneratedNumberPrefix: prefix);
}
