namespace GovErp.Domain.Validation.ValueObjects;

public sealed record VendorSnapshot(Guid VendorId, string Name, bool IsDebarred, bool SamRegistered, bool IsActive = true);
