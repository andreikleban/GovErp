using GovErp.Application.Web.Common;
using GovErp.Application.Web.Ledger.Contracts;

namespace GovErp.Application.Web.Ledger;

/// <summary>
/// Use cases for the journal and fiscal periods.
/// </summary>
public interface ILedgerAppService
{
    Task<IReadOnlyList<JournalEntryVm>> ListJournalAsync(JournalFilter filter, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<FiscalPeriodVm>> ListPeriodsAsync(ActorContext actor, CancellationToken ct = default);

    /// <summary>Budget availability and the financial trial balance of every fund for the business-date fiscal year.</summary>
    Task<IReadOnlyList<FundReportVm>> ListFundReportsAsync(ActorContext actor, CancellationToken ct = default);
}
