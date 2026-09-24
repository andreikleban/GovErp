using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Invoices.Commands;

/// <summary>
/// Command to release a soft stop, with a reason.
/// </summary>
public sealed record OverrideCommand(CommandEnvelope Envelope, Guid InvoiceId, Guid EvaluationId, string RuleId, int? DistributionLine, string Reason);
