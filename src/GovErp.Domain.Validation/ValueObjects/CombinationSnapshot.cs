namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Whether an account combination exists and is active on the date.
/// </summary>
public sealed record CombinationSnapshot(bool Exists, bool IsActiveOnDate, string Status);
