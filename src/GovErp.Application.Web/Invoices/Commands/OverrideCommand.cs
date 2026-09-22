using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

public sealed record OverrideCommand(CommandEnvelope Envelope, Guid InvoiceId, Guid EvaluationId, string RuleId, int? DistributionLine, string Reason);
