using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

/// <summary>
/// Command to place or lift a payment hold.
/// </summary>
public sealed record PaymentHoldCommand(CommandEnvelope Envelope, Guid InvoiceId, bool Hold);
