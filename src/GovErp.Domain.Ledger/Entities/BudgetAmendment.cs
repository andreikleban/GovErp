namespace GovErp.Domain.Ledger.Entities;

public sealed record BudgetAmendment(Money Amount, string Reference, DateOnly EffectiveDate);
