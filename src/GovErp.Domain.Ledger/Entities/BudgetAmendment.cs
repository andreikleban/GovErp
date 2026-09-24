namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// An amendment of the adopted budget, stored on the budget line.
/// </summary>
public sealed record BudgetAmendment(Money Amount, string Reference, DateOnly EffectiveDate);
