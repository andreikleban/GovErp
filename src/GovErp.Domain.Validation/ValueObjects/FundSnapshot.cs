namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// A fund as a validation rule sees it.
/// </summary>
public sealed record FundSnapshot(string Code, string Name, FundKind Kind, BudgetControl Control,
    GrantRule GrantRule, FundRestriction Restriction, bool IsActive);
