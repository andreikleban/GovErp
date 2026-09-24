using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Validation.Contracts;

namespace GovErp.Application.Web.Posting;

/// <summary>
/// Use case that posts an approved invoice.
/// </summary>
public interface IPostingAppService
{
    Task<CommandResult<InvoiceVm>> PostAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);

    /// <summary>Records the payment journal entry when the invoice is ready for handoff. Does not send a bank payment.</summary>
    Task<CommandResult<InvoiceVm>> PayAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PreviewLineVm>> GetJournalAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
}
