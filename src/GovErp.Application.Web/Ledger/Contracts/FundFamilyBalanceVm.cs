namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>
/// Posting balance check for one fund and account family (Budgetary/Financial): debits must equal credits.
/// </summary>
public sealed record FundFamilyBalanceVm(string Fund, string Family, decimal Debit, decimal Credit)
{
    public bool Balanced => Debit == Credit;
}
