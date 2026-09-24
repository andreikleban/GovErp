using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

/// <summary>
/// A command on an invoice that carries no reason text.
/// </summary>
public sealed record InvoiceActionCommand(CommandEnvelope Envelope, Guid InvoiceId);
