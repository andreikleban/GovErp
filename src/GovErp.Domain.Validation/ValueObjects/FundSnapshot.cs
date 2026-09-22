namespace GovErp.Domain.Validation.ValueObjects;

public sealed record FundSnapshot(string Code, string Name, FundKind Kind, BudgetControl Control,
    GrantRule GrantRule, FundRestriction Restriction, bool IsActive);
