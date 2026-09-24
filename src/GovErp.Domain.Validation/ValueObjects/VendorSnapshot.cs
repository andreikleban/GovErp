namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// A vendor as captured on a validation snapshot.
/// </summary>
public sealed record VendorSnapshot(Guid VendorId, string Name, bool IsDebarred, bool SamRegistered, bool IsActive = true);
