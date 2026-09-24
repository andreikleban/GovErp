using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

/// <summary>
/// Command to edit an invoice draft.
/// </summary>
public sealed record UpdateInvoiceCommand(CommandEnvelope Envelope, Guid InvoiceId, string Number, Guid VendorId, DateOnly InvoiceDate,
    DateOnly ServiceDate, DateOnly PostingDate, DateOnly DueDate, decimal Total, string? PoRef, IReadOnlyList<DistributionCommand> Distributions);
