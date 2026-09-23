using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

public sealed record CreateInvoiceCommand(CommandEnvelope Envelope, string Number, Guid VendorId, DateOnly InvoiceDate, DateOnly ServiceDate,
    DateOnly PostingDate, DateOnly DueDate, decimal Total, string? PoRef, IReadOnlyList<DistributionCommand> Distributions,
    string? GeneratedNumberPrefix = null);
