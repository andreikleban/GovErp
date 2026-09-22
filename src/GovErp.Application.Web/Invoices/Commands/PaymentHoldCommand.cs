using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

public sealed record PaymentHoldCommand(CommandEnvelope Envelope, Guid InvoiceId, bool Hold);
