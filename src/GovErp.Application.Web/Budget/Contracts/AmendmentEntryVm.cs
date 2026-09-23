namespace GovErp.Application.Web.Budget.Contracts;

/// <summary>Строка журнала поправок бюджета (Budget › Amendments) — поправка одной строки с указанием её счёта.</summary>
public sealed record AmendmentEntryVm(string Account, int FiscalYear, decimal Amount, string Reference, DateOnly EffectiveDate);
