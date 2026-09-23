namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>Проверка баланса проводки по одному фонду и семейству счетов (Budgetary/Financial): дебет должен равняться кредиту.</summary>
public sealed record FundFamilyBalanceVm(string Fund, string Family, decimal Debit, decimal Credit)
{
    public bool Balanced => Debit == Credit;
}
