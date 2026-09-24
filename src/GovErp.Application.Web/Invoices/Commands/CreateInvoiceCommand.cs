using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

/// <summary>
/// Command to create an invoice draft.
/// </summary>
public sealed record CreateInvoiceCommand(CommandEnvelope Envelope, string Number, Guid VendorId, DateOnly InvoiceDate, DateOnly ServiceDate,
    DateOnly PostingDate, DateOnly DueDate, decimal Total, string? PoRef, IReadOnlyList<DistributionCommand> Distributions,
    string? GeneratedNumberPrefix = null);
