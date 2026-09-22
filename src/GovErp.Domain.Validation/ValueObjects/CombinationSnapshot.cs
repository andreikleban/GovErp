namespace GovErp.Domain.Validation.ValueObjects;

public sealed record CombinationSnapshot(bool Exists, bool IsActiveOnDate, string Status);
