using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;

namespace GovErp.Application.Web.Invoices;

/// <summary>
/// Use cases of the invoice lifecycle.
/// </summary>
public interface IInvoiceAppService
{
    Task<IReadOnlyList<InvoiceListItemVm>> ListAsync(InvoiceListFilter filter, ActorContext actor, CancellationToken ct = default);
    Task<InvoiceVm> GetAsync(Guid id, ActorContext actor, CancellationToken ct = default);
    /// <summary>
    /// The vendor invoice number a new document gets unless it is replaced (nothing is reserved).
    /// </summary>
    Task<string> SuggestNumberAsync(DateOnly postingDate, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> CreateDraftAsync(CreateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> CreateFromPresetAsync(InvoicePreset preset, CommandEnvelope envelope, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> UpdateDraftAsync(UpdateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> ValidateAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> SubmitAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> WithdrawAsync(ReasonedActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> ReturnToDraftAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> SetPaymentHoldAsync(PaymentHoldCommand cmd, ActorContext actor, CancellationToken ct = default);
}
