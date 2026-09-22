using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Validation.Contracts;

namespace GovErp.Application.Web.Posting;

public interface IPostingAppService
{
    Task<CommandResult<InvoiceVm>> PostAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PreviewLineVm>> GetJournalAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
}
