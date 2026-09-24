namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// An encumbrance amount already liquidated, with its source reference.
/// </summary>
public sealed record EncumbranceLiquidation(Money Amount, string SourceRef);
