namespace GovErp.Application.Web.Ledger.Contracts;

public sealed record JournalLineVm(string Account, string Family, decimal Debit, decimal Credit, string Description);
