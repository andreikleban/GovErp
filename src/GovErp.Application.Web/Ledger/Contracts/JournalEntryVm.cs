namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>Проводка журнала. InvoiceId/InvoiceReference — источник (SourceRef совпадает с Reference инвойса), если он ещё виден.</summary>
public sealed record JournalEntryVm(Guid Id, string SourceRef, Guid? InvoiceId, string? InvoiceReference, int PeriodYear, int PeriodMonth,
    DateTimeOffset PostedAt, IReadOnlyList<JournalLineVm> Lines, IReadOnlyList<FundFamilyBalanceVm> Balances);
