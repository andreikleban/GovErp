using GovErp.Application.Web.Common;
using GovErp.Application.Web.Ledger.Contracts;

namespace GovErp.Application.Web.Ledger;

public interface ILedgerAppService
{
    Task<IReadOnlyList<JournalEntryVm>> ListJournalAsync(JournalFilter filter, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<FiscalPeriodVm>> ListPeriodsAsync(ActorContext actor, CancellationToken ct = default);
}
