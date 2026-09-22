using GovErp.Application.Web.Approvals.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;

namespace GovErp.Application.Web.Approvals;

public interface IApprovalAppService
{
    Task<IReadOnlyList<ApprovalQueueItemVm>> GetQueueAsync(ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> ApproveAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> RejectAsync(ReasonedActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> OverrideAsync(OverrideCommand cmd, ActorContext actor, CancellationToken ct = default);
}
