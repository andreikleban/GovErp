using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

/// <summary>
/// A command on an invoice that requires a reason.
/// </summary>
public sealed record ReasonedActionCommand(CommandEnvelope Envelope, Guid InvoiceId, string Reason);
