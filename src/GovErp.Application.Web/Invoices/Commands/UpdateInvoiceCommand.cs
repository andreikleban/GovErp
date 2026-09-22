using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

public sealed record UpdateInvoiceCommand(CommandEnvelope Envelope, Guid InvoiceId, string Number, Guid VendorId, DateOnly InvoiceDate,
    DateOnly ServiceDate, DateOnly PostingDate, DateOnly DueDate, decimal Total, string? PoRef, IReadOnlyList<DistributionCommand> Distributions);
