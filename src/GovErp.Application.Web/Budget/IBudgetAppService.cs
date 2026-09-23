using GovErp.Application.Web.Budget.Commands;
using GovErp.Application.Web.Budget.Contracts;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Budget;

public interface IBudgetAppService
{
    Task<IReadOnlyList<BudgetLineVm>> ListAsync(int fiscalYear, ActorContext actor, CancellationToken ct = default);
    /// <summary>Карточка строки: суммы, поправки, резервы (с номерами инвойсов) и encumbrance на этом счёте. NotFound — неизвестный счёт/год.</summary>
    Task<BudgetLineDetailVm> GetLineAsync(string account, int fiscalYear, ActorContext actor, CancellationToken ct = default);
    /// <summary>Журнал поправок всех строк бюджетного года (Budget › Amendments).</summary>
    Task<IReadOnlyList<AmendmentEntryVm>> ListAmendmentsAsync(int fiscalYear, ActorContext actor, CancellationToken ct = default);
    /// <summary>Все encumbrance тенанта (Budget › Encumbrances), вне зависимости от бюджетного года.</summary>
    Task<IReadOnlyList<EncumbranceVm>> ListEncumbrancesAsync(ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<BudgetLineVm>> AmendAsync(AmendBudgetCommand cmd, ActorContext actor, CancellationToken ct = default);
}
