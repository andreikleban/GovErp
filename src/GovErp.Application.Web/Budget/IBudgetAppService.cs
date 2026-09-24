using GovErp.Application.Web.Budget.Commands;
using GovErp.Application.Web.Budget.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Budget;

/// <summary>
/// Budget use cases.
/// </summary>
public interface IBudgetAppService
{
    Task<IReadOnlyList<BudgetLineVm>> ListAsync(int fiscalYear, ActorContext actor, CancellationToken ct = default);
    /// <summary>
    /// Line card: amounts, amendments, reservations (with invoice numbers) and encumbrances on this account. NotFound for an unknown account/year.
    /// </summary>
    Task<BudgetLineDetailVm> GetLineAsync(string account, int fiscalYear, ActorContext actor, CancellationToken ct = default);
    /// <summary>
    /// Amendment journal of all lines of the budget year (Budget › Amendments).
    /// </summary>
    Task<IReadOnlyList<AmendmentEntryVm>> ListAmendmentsAsync(int fiscalYear, ActorContext actor, CancellationToken ct = default);
    /// <summary>
    /// All tenant encumbrances (Budget › Encumbrances), regardless of the budget year.
    /// </summary>
    Task<IReadOnlyList<EncumbranceVm>> ListEncumbrancesAsync(ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<BudgetLineVm>> AmendAsync(AmendBudgetCommand cmd, ActorContext actor, CancellationToken ct = default);
}
