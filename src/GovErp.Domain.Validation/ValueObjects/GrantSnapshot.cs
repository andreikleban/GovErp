namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// A grant as a validation rule sees it.
/// </summary>
public sealed record GrantSnapshot(string Code, bool IsFederal, GrantEligibilityResult Eligibility, string Status);
