namespace GovErp.Domain.Validation.ValueObjects;

public sealed record GrantSnapshot(string Code, bool IsFederal, GrantEligibilityResult Eligibility, string Status);
