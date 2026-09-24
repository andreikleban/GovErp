namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>A journal entry. InvoiceId/InvoiceReference: the source (SourceRef equals the invoice Reference), if it is still visible.</summary>
public sealed record JournalEntryVm(Guid Id, string SourceRef, Guid? InvoiceId, string? InvoiceReference, int PeriodYear, int PeriodMonth,
    DateTimeOffset PostedAt, IReadOnlyList<JournalLineVm> Lines, IReadOnlyList<FundFamilyBalanceVm> Balances);
